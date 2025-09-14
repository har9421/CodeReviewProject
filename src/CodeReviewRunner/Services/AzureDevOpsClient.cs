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

    public async Task<bool> TestRepositoryAccessAsync(string org, string project, string repoId)
    {
        try
        {
            // Try both with and without project name to see which works
            var urls = new[]
            {
                $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}?api-version=7.0",
                $"{org.TrimEnd('/')}/_apis/git/repositories/{repoId}?api-version=7.0"
            };

            foreach (var url in urls)
            {
                Console.WriteLine($"Testing repository access: {url}");

                var response = await _http.GetAsync(url);
                Console.WriteLine($"Repository access test - Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("name", out var nameProp))
                    {
                        Console.WriteLine($"Repository name: {nameProp.GetString()}");
                    }
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Repository access failed: {response.StatusCode} - {errorContent}");
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception testing repository access: {ex.Message}");
            return false;
        }
    }

    private async Task<string> GetRepositoryNameAsync(string org, string project, string repoId)
    {
        try
        {
            // Try both with and without project name to see which works
            var urls = new[]
            {
                $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}?api-version=7.0",
                $"{org.TrimEnd('/')}/_apis/git/repositories/{repoId}?api-version=7.0"
            };

            foreach (var url in urls)
            {
                var response = await _http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("name", out var nameProp))
                    {
                        return nameProp.GetString() ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception getting repository name: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<List<(string path, string content)>> GetPullRequestChangedFilesAsync(string org, string project, string repoId, string prId)
    {
        // First get the repository name from the repository ID
        var repoName = await GetRepositoryNameAsync(org, project, repoId);
        if (string.IsNullOrEmpty(repoName))
        {
            Console.WriteLine("Could not determine repository name from repository ID");
            return new List<(string path, string content)>();
        }

        Console.WriteLine($"Repository name: {repoName}");

        // Try different URL formats with the repository name
        var urls = new[]
        {
            $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoName}/pullRequests/{prId}/changes?api-version=7.0",
            $"{org.TrimEnd('/')}/_apis/git/repositories/{repoName}/pullRequests/{prId}/changes?api-version=7.0",
            $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/changes?api-version=7.0",
            $"{org.TrimEnd('/')}/_apis/git/repositories/{repoId}/pullRequests/{prId}/changes?api-version=7.0"
        };

        Console.WriteLine($"Organization: {org}");
        Console.WriteLine($"Project: {project}");
        Console.WriteLine($"Repository ID: {repoId}");
        Console.WriteLine($"Pull Request ID: {prId}");

        foreach (var url in urls)
        {
            Console.WriteLine($"Trying: {url}");

            var response = await _http.GetAsync(url);
            Console.WriteLine($"Response Status: {response.StatusCode}");
            Console.WriteLine($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Success with URL: {url}");
                return await ProcessPullRequestChanges(response);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed with {url}: {response.StatusCode} - {errorContent}");

                // Try to get more details about the error
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine("404 Not Found - This could mean:");
                    Console.WriteLine("1. The repository ID is incorrect");
                    Console.WriteLine("2. The pull request ID is incorrect");
                    Console.WriteLine("3. The pull request doesn't exist");
                    Console.WriteLine("4. Insufficient permissions to access the repository/PR");
                    Console.WriteLine("5. The API version is not supported");
                }
            }
        }

        Console.WriteLine("All URL attempts failed");
        return new List<(string path, string content)>();
    }

    private async Task<List<(string path, string content)>> ProcessPullRequestChanges(HttpResponseMessage response)
    {
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

                // For now, just add the path without content to test the API
                result.Add((path, ""));
                Console.WriteLine($"  Added file to analysis list: {path}");
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
            var prUrl = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}?api-version=7.0";
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
            var contentUrl = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}/items?path={Uri.EscapeDataString(filePath)}&version={Uri.EscapeDataString(sourceBranch)}&api-version=7.0";
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
        var url = $"{org}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/threads?api-version=6.0";

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
        var url = $"{org}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/threads?api-version=6.0";

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