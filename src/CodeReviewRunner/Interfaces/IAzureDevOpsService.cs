using CodeReviewRunner.Models;

namespace CodeReviewRunner.Interfaces;

public interface IAzureDevOpsService
{
    Task<List<(string path, string content)>> GetPullRequestChangedFilesAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        CancellationToken cancellationToken = default);

    Task<bool> TestRepositoryAccessAsync(
        string organization,
        string project,
        string repositoryId,
        CancellationToken cancellationToken = default);

    Task PostCommentsAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        string repositoryPath,
        List<CodeIssue> issues,
        IEnumerable<string>? allowedFilePaths = null,
        CancellationToken cancellationToken = default);

    Task PostSummaryAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        List<CodeIssue> issues,
        CancellationToken cancellationToken = default);
}
