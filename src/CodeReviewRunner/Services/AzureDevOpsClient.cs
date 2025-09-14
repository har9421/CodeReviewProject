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

    public async Task<List<string>> GetChangedFilesAsync(string org, string project, string repoId, string prId, string repoPath)
    {
        Console.WriteLine($"GetChangedFilesAsync called with repoPath: '{repoPath}'");
        Console.WriteLine($"Repository path exists: {Directory.Exists(repoPath)}");
        if (Directory.Exists(repoPath))
        {
            Console.WriteLine($"Repository path contents: {string.Join(", ", Directory.GetDirectories(repoPath).Take(5))}");
        }

        // Get latest iteration id
        var iterationsUrl = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/iterations?api-version=7.0";
        Console.WriteLine($"Fetching iterations from: {iterationsUrl}");
        var iterRes = await _http.GetAsync(iterationsUrl);
        if (!iterRes.IsSuccessStatusCode)
        {
            var errorContent = await iterRes.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to fetch iterations: {iterRes.StatusCode} - {errorContent}");
            return new List<string>();
        }
        var iterJson = await iterRes.Content.ReadAsStringAsync();

        int latestIterationId = 0;
        using (var iterDoc = System.Text.Json.JsonDocument.Parse(iterJson))
        {
            if (iterDoc.RootElement.TryGetProperty("value", out var iterations))
            {
                foreach (var it in iterations.EnumerateArray())
                {
                    if (it.TryGetProperty("id", out var idEl))
                    {
                        var id = idEl.GetInt32();
                        if (id > latestIterationId) latestIterationId = id;
                    }
                }
            }
        }
        if (latestIterationId == 0)
            return new List<string>();

        var url = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/iterations/{latestIterationId}/changes?api-version=7.0";
        Console.WriteLine($"Fetching changes from: {url}");
        var res = await _http.GetAsync(url);
        var files = new List<string>();

        if (res.IsSuccessStatusCode)
        {
            var json = await res.Content.ReadAsStringAsync();
            using (var doc = System.Text.Json.JsonDocument.Parse(json))
            {
                // Some endpoints return 'changes', others 'value'
                if (!doc.RootElement.TryGetProperty("changes", out var changes) && !doc.RootElement.TryGetProperty("value", out changes))
                {
                    changes = default;
                }
                if (changes.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (change.TryGetProperty("changeType", out var changeTypeEl))
                        {
                            var changeType = changeTypeEl.GetString();
                            if (string.Equals(changeType, "delete", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        string? path = null;
                        if (change.TryGetProperty("item", out var item) && item.TryGetProperty("path", out var pathEl))
                        {
                            path = pathEl.GetString();
                        }
                        if (string.IsNullOrWhiteSpace(path) && change.TryGetProperty("originalPath", out var origPathEl))
                        {
                            path = origPathEl.GetString();
                        }
                        if (string.IsNullOrWhiteSpace(path))
                            continue;

                        // Normalize the path by removing leading slashes and backslashes
                        var normalizedPath = path.TrimStart('/', '\\');
                        var combined = Path.Combine(repoPath, normalizedPath);
                        Console.WriteLine($"  Original path: '{path}' -> Normalized: '{normalizedPath}' -> Combined: '{combined}'");
                        files.Add(combined);
                    }
                }
            }
        }
        else
        {
            var errorContent = await res.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to fetch changes: {res.StatusCode} - {errorContent}");
        }

        if (files.Count == 0)
        {
            // Fallback via commits aggregation
            var commitsUrl = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/commits?api-version=7.0";
            Console.WriteLine($"Fallback: Fetching commits from: {commitsUrl}");
            var commitsRes = await _http.GetAsync(commitsUrl);
            if (!commitsRes.IsSuccessStatusCode)
            {
                var errorContent = await commitsRes.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to fetch commits: {commitsRes.StatusCode} - {errorContent}");
                return files;
            }
            var commitsJson = await commitsRes.Content.ReadAsStringAsync();
            using var commitsDoc = System.Text.Json.JsonDocument.Parse(commitsJson);
            if (commitsDoc.RootElement.TryGetProperty("value", out var commitsArr))
            {
                foreach (var commit in commitsArr.EnumerateArray())
                {
                    var commitId = commit.TryGetProperty("commitId", out var idEl) ? idEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(commitId)) continue;
                    var chUrl = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}/commits/{commitId}/changes?api-version=7.0";
                    var chRes = await _http.GetAsync(chUrl);
                    if (!chRes.IsSuccessStatusCode) continue;
                    var chJson = await chRes.Content.ReadAsStringAsync();
                    using var chDoc = System.Text.Json.JsonDocument.Parse(chJson);
                    if (chDoc.RootElement.TryGetProperty("changes", out var chChanges) || chDoc.RootElement.TryGetProperty("value", out chChanges))
                    {
                        foreach (var change in chChanges.EnumerateArray())
                        {
                            if (change.TryGetProperty("changeType", out var changeTypeEl))
                            {
                                var changeType = changeTypeEl.GetString();
                                if (string.Equals(changeType, "delete", StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }
                            if (change.TryGetProperty("item", out var item) && item.TryGetProperty("path", out var pathEl))
                            {
                                var path = pathEl.GetString();
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    // Normalize the path by removing leading slashes and backslashes
                                    var normalizedPath = path.TrimStart('/', '\\');
                                    var combined = Path.Combine(repoPath, normalizedPath);
                                    Console.WriteLine($"  Fallback - Original path: '{path}' -> Normalized: '{normalizedPath}' -> Combined: '{combined}'");
                                    files.Add(combined);
                                }
                            }
                        }
                    }
                }
            }
        }

        return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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