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
        // Get latest iteration id
        var iterationsUrl = $"{org}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/iterations?api-version=7.1-preview.1";
        var iterRes = await _http.GetAsync(iterationsUrl);
        iterRes.EnsureSuccessStatusCode();
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

        var url = $"{org}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/iterations/{latestIterationId}/changes?api-version=7.1-preview.1";
        var res = await _http.GetAsync(url);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();

        var files = new List<string>();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("changes", out var changes))
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
                // For renames, sometimes originalPath exists
                if (string.IsNullOrWhiteSpace(path) && change.TryGetProperty("originalPath", out var origPathEl))
                {
                    path = origPathEl.GetString();
                }

                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var combined = Path.Combine(repoPath, path.TrimStart('/', '\\'));
                files.Add(combined);
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
                    continue;
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
            Console.WriteLine($"Posted comment: {res.StatusCode}");
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