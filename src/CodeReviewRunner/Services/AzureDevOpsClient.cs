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

    public async Task PostCommentsAsync(string org, string project, string repoId, string prId, string repoPath, List<CodeIssue> issues)
    {
        var url = $"{org}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/threads?api-version=6.0";

        foreach (var issue in issues)
        {
            var relativePath =
                Path.DirectorySeparatorChar == '/'
                ? Path.GetRelativePath(repoPath, issue.FilePath)
                : Path.GetRelativePath(repoPath, issue.FilePath).Replace('\\', '/');
            if (!relativePath.StartsWith('/'))
                relativePath = "/" + relativePath;

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
}