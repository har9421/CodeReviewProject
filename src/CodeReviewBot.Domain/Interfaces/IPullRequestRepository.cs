using CodeReviewBot.Domain.Entities;

namespace CodeReviewBot.Domain.Interfaces;

public interface IPullRequestRepository
{
    Task<PullRequest?> GetPullRequestDetailsAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken);
    Task<List<FileChange>> GetPullRequestChangesAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken);
    Task<bool> PostCommentAsync(string organizationUrl, string projectName, string repositoryName, int pullRequestId, string personalAccessToken, PullRequestComment comment);
}

public class PullRequestComment
{
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Severity { get; set; } = string.Empty;
}
