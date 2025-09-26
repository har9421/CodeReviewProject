using CodeReviewBot.Models;

namespace CodeReviewBot.Interfaces;

public interface ICodeAnalyzerService
{
    Task<List<CodeIssue>> AnalyzeFileAsync(FileChange fileChange);
}
