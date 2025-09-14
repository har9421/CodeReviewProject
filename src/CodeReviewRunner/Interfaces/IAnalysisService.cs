using CodeReviewRunner.Models;

namespace CodeReviewRunner.Interfaces;

public interface IAnalysisService
{
    Task<List<CodeIssue>> AnalyzeCSharpFilesAsync(
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default);

    Task<List<CodeIssue>> AnalyzeJavaScriptFilesAsync(
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default);

    Task<List<CodeIssue>> AnalyzeFilesAsync(
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default);
}
