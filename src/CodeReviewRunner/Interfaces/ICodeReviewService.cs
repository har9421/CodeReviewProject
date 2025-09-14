using CodeReviewRunner.Models;

namespace CodeReviewRunner.Interfaces;

public interface ICodeReviewService
{
    Task<CodeReviewResult> AnalyzePullRequestAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        CancellationToken cancellationToken = default);

    Task<CodeReviewResult> AnalyzeLocalFilesAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default);
}

public class CodeReviewResult
{
    public bool Success { get; set; }
    public List<CodeIssue> Issues { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public int FilesAnalyzed { get; set; }
    public int TotalIssues => Issues.Count;
    public int ErrorCount => Issues.Count(i => i.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
    public int WarningCount => Issues.Count(i => i.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));
}
