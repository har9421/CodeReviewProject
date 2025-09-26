using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CodeReviewBot.Infrastructure.ExternalServices;

public class AzureDevOpsService : IPullRequestRepository
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureDevOpsService> _logger;

    public AzureDevOpsService(HttpClient httpClient, ILogger<AzureDevOpsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PullRequest?> GetPullRequestDetailsAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken)
    {
        try
        {
            _logger.LogInformation("Fetching PR {PullRequestId} details from {Project}/{Repository}", pullRequestId, projectName, repositoryName);

            var baseUrl = organizationUrl.TrimEnd('/');
            var prUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}?api-version=7.0";

            _logger.LogInformation("Making request to: {PrUrl}", prUrl);

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var response = await _httpClient.GetAsync(prUrl);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var prDetails = JsonSerializer.Deserialize<PullRequestDetailsResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (prDetails == null)
                return null;

            return new PullRequest
            {
                PullRequestId = prDetails.PullRequestId,
                Title = prDetails.Title,
                Description = prDetails.Description,
                Status = prDetails.Status,
                SourceRefName = prDetails.SourceRefName,
                TargetRefName = prDetails.TargetRefName,
                RepositoryName = repositoryName,
                ProjectName = projectName,
                OrganizationUrl = organizationUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch PR {PullRequestId} details", pullRequestId);
            return null;
        }
    }

    public async Task<List<FileChange>> GetPullRequestChangesAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken)
    {
        var fileChanges = new List<FileChange>();
        try
        {
            _logger.LogInformation("Fetching changes for PR {PullRequestId}", pullRequestId);

            var baseUrl = organizationUrl.TrimEnd('/');
            var changesUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/changes?api-version=7.0";

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var response = await _httpClient.GetAsync(changesUrl);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var changesResponse = JObject.Parse(jsonContent);

            foreach (var change in changesResponse["changes"] ?? new JArray())
            {
                var item = change["item"]?["path"]?.ToString();
                var changeType = change["changeType"]?.ToString();
                var url = change["item"]?["url"]?.ToString();

                if (!string.IsNullOrEmpty(item) && item.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var content = await GetFileContentAsync(organizationUrl, projectName, repositoryName, item, changesResponse["baseVersion"]?.ToString() ?? "", personalAccessToken);
                    fileChanges.Add(new FileChange { Path = item, ChangeType = changeType ?? "edit", Content = content });
                }
            }

            _logger.LogInformation("Found {FileCount} C# file changes for PR {PullRequestId}", fileChanges.Count, pullRequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch changes for PR {PullRequestId}", pullRequestId);
        }
        return fileChanges;
    }

    public async Task<bool> PostCommentAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken, PullRequestComment comment)
    {
        try
        {
            _logger.LogInformation("Posting comment to PR {PullRequestId}", pullRequestId);

            var baseUrl = organizationUrl.TrimEnd('/');
            var commentUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/threads?api-version=7.0";

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var commentPayload = new
            {
                comments = new[]
                {
                    new
                    {
                        content = comment.Content,
                        commentType = "text",
                        parentCommentId = 0,
                        lastContentUpdatedDate = DateTime.UtcNow,
                        lastUpdatedDate = DateTime.UtcNow,
                        publishedDate = DateTime.UtcNow,
                        isDeleted = false,
                        author = new { displayName = "Code Review Bot" }
                    }
                },
                status = "active",
                threadContext = new
                {
                    filePath = comment.FilePath,
                    rightFileEnd = new { line = comment.LineNumber, column = 1 },
                    rightFileStart = new { line = comment.LineNumber, column = 1 }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(commentPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(commentUrl, content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Successfully posted comment to PR {PullRequestId}", pullRequestId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post comment to PR {PullRequestId}", pullRequestId);
            return false;
        }
    }

    private async Task<string?> GetFileContentAsync(string organizationUrl, string projectName, string repositoryName, string filePath, string commitId, string personalAccessToken)
    {
        try
        {
            var baseUrl = organizationUrl.TrimEnd('/');
            var contentUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/items?path={Uri.EscapeDataString(filePath)}&version={commitId}&api-version=7.0";

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var response = await _httpClient.GetAsync(contentUrl);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file content for {FilePath} at commit {CommitId}", filePath, commitId);
            return null;
        }
    }
}

public class PullRequestDetailsResponse
{
    public int PullRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceRefName { get; set; } = string.Empty;
    public string TargetRefName { get; set; } = string.Empty;
}
