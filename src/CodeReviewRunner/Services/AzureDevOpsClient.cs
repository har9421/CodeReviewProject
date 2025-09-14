using System.Net.Http.Headers;
using CodeReviewRunner.Models;

namespace CodeReviewRunner.Services;

public class AzureDevOpsClient
{
    private readonly HttpClient _http;

    public AzureDevOpsClient(string pat)
    {
        _http = new HttpClient();
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($":{pat}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    public async Task<List<(string path, string content)>> GetPullRequestChangedFilesAsync(string org, string project, string repoId, string prId)
    {
        var url = $"{org.TrimEnd('/')}/_apis/git/repositories/{repoId}/pullRequests/{prId}/changes?api-version=7.0";
        Console.WriteLine($"Fetching PR changes from: {url}");

        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to fetch PR changes: {response.StatusCode} - {errorContent}");
            return new List<(string path, string content)>();
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        var result = new List<(string path, string content)>();

        if (doc.RootElement.TryGetProperty("changes", out var changes))
        {
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("item", out var item)) continue;
                if (!item.TryGetProperty("path", out var pathProp)) continue;

                var path = pathProp.GetString();
                if (string.IsNullOrEmpty(path)) continue;

                // Check if it's a file we want to analyze (C# or JS/TS files)
                var isAnalyzableFile = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                     path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                                     path.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ||
                                     path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                                     path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase);

                if (!isAnalyzableFile) continue;

                // Skip deleted files
                if (change.TryGetProperty("changeType", out var changeTypeEl))
                {
                    var changeType = changeTypeEl.GetString();
                    if (string.Equals(changeType, "delete", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                Console.WriteLine($"  Found changed file: {path}");

                // Fetch actual file content from the PR
                var content = await GetFileContentAsync(org, project, repoId, prId, path);
                if (!string.IsNullOrEmpty(content))
                {
                    result.Add((path, content));
                    Console.WriteLine($"  Successfully fetched content for: {path} ({content.Length} characters)");
                }
                else
                {
                    Console.WriteLine($"  Failed to fetch content for: {path}");
                }
            }
        }

        Console.WriteLine($"Total analyzable files found: {result.Count}");
        return result;
    }

    private async Task<string> GetFileContentAsync(string org, string project, string repoId, string prId, string filePath)
    {
        try
        {
            // Get the PR details to find the source branch
            var prUrl = $"{org.TrimEnd('/')}/_apis/git/repositories/{repoId}/pullRequests/{prId}?api-version=7.0";
            var prResponse = await _http.GetAsync(prUrl);
            if (!prResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch PR details: {prResponse.StatusCode}");
                return string.Empty;
            }

            var prJson = await prResponse.Content.ReadAsStringAsync();
            using var prDoc = System.Text.Json.JsonDocument.Parse(prJson);

            string? sourceBranch = null;
            if (prDoc.RootElement.TryGetProperty("sourceRefName", out var sourceRefEl))
            {
                sourceBranch = sourceRefEl.GetString()?.Replace("refs/heads/", "");
            }

            if (string.IsNullOrEmpty(sourceBranch))
            {
                Console.WriteLine("Could not determine source branch");
                return string.Empty;
            }

            // Get the file content from the source branch
            var contentUrl = $"{org.TrimEnd('/')}/_apis/git/repositories/{repoId}/items?path={Uri.EscapeDataString(filePath)}&version={Uri.EscapeDataString(sourceBranch)}&api-version=7.0";
            var contentResponse = await _http.GetAsync(contentUrl);

            if (contentResponse.IsSuccessStatusCode)
            {
                return await contentResponse.Content.ReadAsStringAsync();
            }
            else
            {
                Console.WriteLine($"Failed to fetch file content for {filePath}: {contentResponse.StatusCode}");
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while fetching file content for {filePath}: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task PostCommentsAsync(string org, string project, string repoId, string prId, string repoPath, List<CodeIssue> issues, IEnumerable<string>? allowedFilePaths = null)
    {
        var url = $"{org}/_apis/git/repositories/{repoId}/pullRequests/{prId}/threads?api-version=6.0";

        var allowedSet = allowedFilePaths != null
            ? new HashSet<string>(allowedFilePaths.Select(p => NormalizeForCompare(repoPath, p)), StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var issue in issues)
        {
            var relativePath =
                Path.DirectorySeparatorChar == '/'
                ? Path.GetRelativePath(repoPath, issue.FilePath)
                : Path.GetRelativePath(repoPath, issue.FilePath).Replace('\\', '/');
            if (!relativePath.StartsWith('/'))
                relativePath = "/" + relativePath;

            if (allowedSet != null)
            {
                var normalizedIssuePath = NormalizeForCompare(repoPath, Path.Combine(repoPath, relativePath.TrimStart('/')));
                if (!allowedSet.Contains(normalizedIssuePath))
                {
                    Console.WriteLine($"Skip commenting on {relativePath} (not in PR changed files)");
                    continue;
                }
            }

            var body = new
            {
                comments = new[] {
                    new { parentCommentId = 0, content = $"{issue.Severity.ToUpper()}: {issue.Message} (rule {issue.RuleId})", commentType = "text" }
                },
                status = "active",
                threadContext = new
                {
                    filePath = relativePath,
                    rightFileStart = new { line = issue.Line, offset = 1 },
                    rightFileEnd = new { line = issue.Line, offset = 1 }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var res = await _http.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
            Console.WriteLine($"Post comment on {relativePath} line {issue.Line}: {res.StatusCode}");
        }
    }

    public async Task PostSummaryAsync(string org, string project, string repoId, string prId, List<CodeIssue> issues)
    {
        var url = $"{org}/_apis/git/repositories/{repoId}/pullRequests/{prId}/threads?api-version=6.0";

        var errorCount = issues.Count(i => i.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        var warnCount = issues.Count(i => i.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));
        var byLang = issues
            .GroupBy(i => i.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "C#" : (i.FilePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) || i.FilePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) || i.FilePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || i.FilePath.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ? "JS/TS" : "Other"))
            .Select(g => $"- {g.Key}: {g.Count()} issues");

        var content = $"[CodeReview Bot] Summary\n\n" +
                      $"- Errors: {errorCount}\n" +
                      $"- Warnings: {warnCount}\n" +
                      string.Join("\n", byLang.Take(10));

        var body = new
        {
            comments = new[] {
                new { parentCommentId = 0, content = content, commentType = "text" }
            },
            status = "active"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var res = await _http.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Console.WriteLine($"Posted summary: {res.StatusCode}");
    }
    private static string NormalizeForCompare(string repoPath, string fullPath)
    {
        var normalized = fullPath;
        if (Path.DirectorySeparatorChar == '\\')
            normalized = normalized.Replace('/', '\\');
        else
            normalized = normalized.Replace('\\', '/');
        var rel = Path.GetRelativePath(repoPath, normalized);
        return rel.TrimStart('/', '\\');
    }
}