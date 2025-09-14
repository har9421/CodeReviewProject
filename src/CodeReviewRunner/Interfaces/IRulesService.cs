using CodeReviewRunner.Models;

namespace CodeReviewRunner.Interfaces;

public interface IRulesService
{
    Task<List<CodingRule>> GetRulesAsync(CancellationToken cancellationToken = default);
    Task<List<CodingRule>> GetRulesFromUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<bool> ValidateRulesAsync(List<CodingRule> rules, CancellationToken cancellationToken = default);
    Task CacheRulesAsync(List<CodingRule> rules, CancellationToken cancellationToken = default);
    Task<List<CodingRule>?> GetCachedRulesAsync(CancellationToken cancellationToken = default);
}
