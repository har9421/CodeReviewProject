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
}