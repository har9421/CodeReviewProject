using CodeReviewBot.Domain.Entities;

namespace CodeReviewBot.Domain.Interfaces;

public interface ICodeAnalyzer
{
    Task<List<CodeIssue>> AnalyzeFileAsync(FileChange fileChange);
    Task<List<CodingRule>> LoadCodingRulesAsync();
}
