using CodeReviewRunner.Models;
using Newtonsoft.Json.Linq;

namespace CodeReviewRunner.Interfaces;

public interface IAnalysisService
{
    Task<List<CodeIssue>> AnalyzeCSharpFilesAsync(
        JObject rules,
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default);

    Task<List<CodeIssue>> AnalyzeFilesAsync(
        JObject rules,
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default);
}
