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

            // Create a request message with proper authorization header
            var request = new HttpRequestMessage(HttpMethod.Get, prUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var response = await _httpClient.SendAsync(request);

            _logger.LogInformation("Received HTTP response status: {StatusCode}", response.StatusCode);

            // Log response content for debugging
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Response content: {ResponseContent}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("API request failed with status {StatusCode}. Response: {ResponseContent}",
                    response.StatusCode, responseContent);
                return null;
            }

            // Check if response is HTML (indicates authentication issue)
            if (responseContent.TrimStart().StartsWith("<"))
            {
                _logger.LogError("Received HTML response instead of JSON. This usually indicates an authentication issue. Response: {ResponseContent}",
                    responseContent);
                return null;
            }

            var prDetails = JsonSerializer.Deserialize<PullRequestDetailsResponse>(responseContent, new JsonSerializerOptions
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
            // Get PR details first to get commit IDs
            var prUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}?api-version=7.0";

            var prRequest = new HttpRequestMessage(HttpMethod.Get, prUrl);
            prRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var prResponse = await _httpClient.SendAsync(prRequest);

            if (!prResponse.IsSuccessStatusCode)
            {
                var errorContent = await prResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get PR details. Status: {StatusCode}, Response: {Response}",
                    prResponse.StatusCode, errorContent);
                return fileChanges;
            }

            var prJson = await prResponse.Content.ReadAsStringAsync();
            var prData = JObject.Parse(prJson);

            var sourceCommitId = prData["lastMergeSourceCommit"]?["commitId"]?.ToString();
            var targetCommitId = prData["lastMergeTargetCommit"]?["commitId"]?.ToString();

            if (string.IsNullOrEmpty(sourceCommitId) || string.IsNullOrEmpty(targetCommitId))
            {
                _logger.LogWarning("Could not get source or target commit IDs for PR {PullRequestId}", pullRequestId);
                return fileChanges;
            }

            // Get all commits in the PR first
            var commitsUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/commits?api-version=7.0";

            var commitsRequest = new HttpRequestMessage(HttpMethod.Get, commitsUrl);
            commitsRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var commitsResponse = await _httpClient.SendAsync(commitsRequest);

            if (!commitsResponse.IsSuccessStatusCode)
            {
                var errorContent = await commitsResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get commits. Status: {StatusCode}, Response: {Response}",
                    commitsResponse.StatusCode, errorContent);
                return fileChanges;
            }

            var commitsJson = await commitsResponse.Content.ReadAsStringAsync();
            var commitsData = JObject.Parse(commitsJson);
            var commits = commitsData["value"] as JArray;

            var allFilePaths = new HashSet<string>();

            // Get changes from each commit
            if (commits != null)
            {
                foreach (var commit in commits)
                {
                    var commitId = commit["commitId"]?.ToString();
                    if (!string.IsNullOrEmpty(commitId))
                    {
                        var changesUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/commits/{commitId}/changes?api-version=7.0";

                        var changesRequest = new HttpRequestMessage(HttpMethod.Get, changesUrl);
                        changesRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

                        var response = await _httpClient.SendAsync(changesRequest);
                        if (response.IsSuccessStatusCode)
                        {
                            var jsonContent = await response.Content.ReadAsStringAsync();
                            var changesResponse = JObject.Parse(jsonContent);
                            var changes = changesResponse["changes"] as JArray;

                            if (changes != null)
                            {
                                foreach (var change in changes)
                                {
                                    var item = change["item"]?["path"]?.ToString();
                                    var changeType = change["changeType"]?.ToString();
                                    if (!string.IsNullOrEmpty(item) && item.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                                    {
                                        allFilePaths.Add(item);
                                        _logger.LogInformation("Found changed C# file: {FilePath} (ChangeType: {ChangeType})", item, changeType);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Now get content for each unique file
            foreach (var filePath in allFilePaths)
            {
                // Skip Program.cs files from analysis
                if (filePath.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Skipping Program.cs file from analysis: {FilePath}", filePath);
                    continue;
                }

                var cleanPath = filePath.TrimStart('/');
                var content = await GetFileContentAsync(organizationUrl, projectName, repositoryName, cleanPath, sourceCommitId, personalAccessToken);

                // Get actual changed lines by calling the diff API
                var changedLines = await GetActualChangedLinesAsync(organizationUrl, projectName, repositoryName, cleanPath, targetCommitId, sourceCommitId, personalAccessToken);

                fileChanges.Add(new FileChange
                {
                    Path = filePath,
                    ChangeType = "edit",
                    Content = content,
                    ChangedLines = changedLines,
                    AnalyzeOnlyChangedLines = true // Only analyze changed lines
                });
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

            // Create comment payload - handle both file-specific and general comments
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
                threadContext = string.IsNullOrEmpty(comment.FilePath) || comment.LineNumber <= 0
                    ? null
                    : new
                    {
                        filePath = comment.FilePath,
                        rightFileEnd = new { line = comment.LineNumber, offset = 1 },
                        rightFileStart = new { line = comment.LineNumber, offset = 1 }
                    }
            };

            var jsonPayload = JsonSerializer.Serialize(commentPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, commentUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to post comment. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Response status code does not indicate success: {response.StatusCode}.");
            }

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
            // Try getting the file from the latest version first
            var contentUrl = $"{baseUrl}/{projectName}/_apis/git/repositories/{repositoryName}/items?path={Uri.EscapeDataString(filePath)}&api-version=7.0";

            var request = new HttpRequestMessage(HttpMethod.Get, contentUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get file content. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file content for {FilePath} at commit {CommitId}", filePath, commitId);
            return null;
        }
    }

    private async Task<List<int>> GetActualChangedLinesAsync(string organizationUrl, string projectName, string repositoryName, string filePath, string baseCommitId, string targetCommitId, string personalAccessToken)
    {
        try
        {
            // For now, implement a simpler approach: if the commits are different, assume all lines are changed
            // This is a temporary solution until we can properly parse the Azure DevOps diff API
            if (baseCommitId != targetCommitId)
            {
                _logger.LogInformation("Commits are different for file {FilePath}, analyzing entire file as changed", filePath);

                // Get the file content to determine line count
                var content = await GetFileContentAsync(organizationUrl, projectName, repositoryName, filePath, targetCommitId, personalAccessToken);
                if (!string.IsNullOrEmpty(content))
                {
                    var lines = content.Split('\n');
                    var allLines = new List<int>();
                    for (int i = 1; i <= lines.Length; i++)
                    {
                        allLines.Add(i);
                    }
                    _logger.LogInformation("Found {LineCount} lines in file {FilePath} (treating all as changed)", allLines.Count, filePath);
                    return allLines;
                }
            }
            else
            {
                _logger.LogInformation("Commits are identical for file {FilePath}, no changes detected", filePath);
            }

            return new List<int>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get changed lines for {FilePath} between commits {BaseCommitId} and {TargetCommitId}", filePath, baseCommitId, targetCommitId);
            return new List<int>();
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
