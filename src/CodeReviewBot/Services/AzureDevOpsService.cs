using CodeReviewBot.Configuration;
using CodeReviewBot.Interfaces;
using CodeReviewBot.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CodeReviewBot.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly BotOptions _botOptions;

    public AzureDevOpsService(
        HttpClient httpClient,
        ILogger<AzureDevOpsService> logger,
        IOptions<BotOptions> botOptions)
    {
        _httpClient = httpClient;
        _logger = logger;
        _botOptions = botOptions.Value;
    }

    public async Task<PullRequestDetails?> GetPullRequestDetailsAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken)
    {
        try
        {
            _logger.LogInformation("Fetching PR {PullRequestId} details from {Project}/{Repository}", pullRequestId, projectName, repositoryName);

            // Ensure organizationUrl doesn't end with slash to avoid double slashes
            var baseUrl = organizationUrl.TrimEnd('/');
            var prUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}?api-version=7.0";

            _logger.LogInformation("Making request to: {PrUrl}", prUrl);
            _logger.LogInformation("PAT length: {PatLength}", personalAccessToken?.Length ?? 0);

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var response = await _httpClient.GetAsync(prUrl);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var prDetails = JsonSerializer.Deserialize<PullRequestDetails>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Successfully fetched PR {PullRequestId} details", pullRequestId);
            return prDetails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch PR {PullRequestId} details", pullRequestId);
            return null;
        }
    }

    public async Task<List<FileChange>> GetPullRequestChangesAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken)
    {
        try
        {
            _logger.LogInformation("Fetching changes for PR {PullRequestId}", pullRequestId);

            // Ensure organizationUrl doesn't end with slash to avoid double slashes
            var baseUrl = organizationUrl.TrimEnd('/');
            var changesUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/changes?api-version=7.0";

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var response = await _httpClient.GetAsync(changesUrl);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var changesResponse = JsonSerializer.Deserialize<ChangesResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var fileChanges = new List<FileChange>();

            if (changesResponse?.ChangeEntries != null)
            {
                foreach (var change in changesResponse.ChangeEntries)
                {
                    if (change.Item?.Path != null && IsCSharpFile(change.Item.Path))
                    {
                        var fileContent = await GetFileContentAsync(organizationUrl, projectName, repositoryName, change.Item.Path, change.Item.CommitId, personalAccessToken);

                        fileChanges.Add(new FileChange
                        {
                            Path = change.Item.Path,
                            ChangeType = change.ChangeType ?? "Unknown",
                            Content = fileContent,
                            CommitId = change.Item.CommitId
                        });
                    }
                }
            }

            _logger.LogInformation("Found {FileCount} C# files changed in PR {PullRequestId}", fileChanges.Count, pullRequestId);
            return fileChanges;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch changes for PR {PullRequestId}", pullRequestId);
            return new List<FileChange>();
        }
    }

    public async Task<bool> PostCommentAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken, PullRequestComment comment)
    {
        try
        {
            _logger.LogInformation("Posting comment to PR {PullRequestId}", pullRequestId);

            // Ensure organizationUrl doesn't end with slash to avoid double slashes
            var baseUrl = organizationUrl.TrimEnd('/');
            var commentUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/threads?api-version=7.0";

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var threadPayload = new
            {
                comments = new[]
                {
                    new
                    {
                        parentCommentId = 0,
                        content = comment.Content,
                        commentType = 1
                    }
                },
                status = 1,
                threadContext = new
                {
                    filePath = comment.FilePath,
                    leftFileStart = comment.LineNumber > 0 ? new { line = comment.LineNumber, offset = 1 } : null,
                    leftFileEnd = comment.LineNumber > 0 ? new { line = comment.LineNumber, offset = 1 } : null,
                    rightFileStart = comment.LineNumber > 0 ? new { line = comment.LineNumber, offset = 1 } : null,
                    rightFileEnd = comment.LineNumber > 0 ? new { line = comment.LineNumber, offset = 1 } : null
                }
            };

            var jsonContent = JsonSerializer.Serialize(threadPayload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

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
            // Ensure organizationUrl doesn't end with slash to avoid double slashes
            var baseUrl = organizationUrl.TrimEnd('/');
            var contentUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/items?path={Uri.EscapeDataString(filePath)}&version={commitId}&api-version=7.0";

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var response = await _httpClient.GetAsync(contentUrl);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch content for file {FilePath}", filePath);
            return null;
        }
    }

    private static bool IsCSharpFile(string filePath)
    {
        return filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }
}
