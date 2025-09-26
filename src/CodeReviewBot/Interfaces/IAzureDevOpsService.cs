using CodeReviewBot.Models;

namespace CodeReviewBot.Interfaces;

public interface IAzureDevOpsService
{
    Task<PullRequestDetails?> GetPullRequestDetailsAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken);
    Task<List<FileChange>> GetPullRequestChangesAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken);
    Task<bool> PostCommentAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken, PullRequestComment comment);
}
