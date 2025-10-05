using CodeReviewBot.Domain.Entities;

namespace CodeReviewBot.Domain.Interfaces;

public interface ILearningService
{
    Task RecordPullRequestAnalysisAsync(LearningData learningData);
    Task<List<AdaptiveRule>> GetAdaptiveRulesAsync();
    Task UpdateRuleEffectivenessAsync(string ruleId, FeedbackType feedback);
    Task<List<RuleEffectiveness>> GetRuleEffectivenessAsync();
    Task<double> CalculateRuleConfidenceAsync(string ruleId);
    Task<List<CodeIssue>> FilterIssuesByRelevanceAsync(List<CodeIssue> issues, string repositoryName);
    Task<LearningInsights> GetLearningInsightsAsync();
    Task OptimizeRulesAsync();
}

public class LearningInsights
{
    public int TotalPullRequestsAnalyzed { get; set; }
    public int TotalIssuesFound { get; set; }
    public double AverageIssuesPerPR { get; set; }
    public double AverageRuleEffectiveness { get; set; }
    public List<string> MostEffectiveRules { get; set; } = new();
    public List<string> LeastEffectiveRules { get; set; } = new();
    public Dictionary<string, double> RuleEffectivenessScores { get; set; } = new();
    public Dictionary<string, int> FileTypeIssueDistribution { get; set; } = new();
    public double DeveloperSatisfactionScore { get; set; }
    public List<string> RecommendedRuleAdjustments { get; set; } = new();
}
