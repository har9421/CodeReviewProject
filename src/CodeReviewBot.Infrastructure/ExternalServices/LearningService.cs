using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeReviewBot.Infrastructure.ExternalServices;

public class LearningService : ILearningService
{
    private readonly ILogger<LearningService> _logger;
    private readonly string _learningDataPath = "learning-data.json";
    private readonly string _adaptiveRulesPath = "adaptive-rules.json";
    private List<LearningData> _learningData = new();
    private List<AdaptiveRule> _adaptiveRules = new();
    private readonly object _lockObject = new();

    public LearningService(ILogger<LearningService> logger)
    {
        _logger = logger;
        _ = Task.Run(LoadLearningDataAsync);
    }

    public async Task RecordPullRequestAnalysisAsync(LearningData learningData)
    {
        try
        {
            lock (_lockObject)
            {
                _learningData.Add(learningData);
            }

            await SaveLearningDataAsync();
            await UpdateRuleEffectivenessFromDataAsync(learningData);

            _logger.LogInformation("Recorded learning data for PR {PullRequestId}", learningData.PullRequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record learning data for PR {PullRequestId}", learningData.PullRequestId);
        }
    }

    public async Task<List<AdaptiveRule>> GetAdaptiveRulesAsync()
    {
        if (!_adaptiveRules.Any())
        {
            await LoadAdaptiveRulesAsync();
        }
        return _adaptiveRules.Where(r => r.IsActive).ToList();
    }

    public async Task UpdateRuleEffectivenessAsync(string ruleId, FeedbackType feedback)
    {
        try
        {
            var ruleEffectiveness = _learningData
                .SelectMany(ld => ld.RuleEffectiveness)
                .FirstOrDefault(re => re.RuleId == ruleId);

            if (ruleEffectiveness == null)
            {
                ruleEffectiveness = new RuleEffectiveness
                {
                    RuleId = ruleId,
                    LastUpdated = DateTime.UtcNow
                };

                // Add to the most recent learning data entry
                var latestData = _learningData.OrderByDescending(ld => ld.AnalysisDate).FirstOrDefault();
                if (latestData != null)
                {
                    latestData.RuleEffectiveness.Add(ruleEffectiveness);
                }
            }

            switch (feedback)
            {
                case FeedbackType.Accepted:
                case FeedbackType.TruePositive:
                    ruleEffectiveness.IssuesAccepted++;
                    break;
                case FeedbackType.Rejected:
                case FeedbackType.FalsePositive:
                    ruleEffectiveness.IssuesRejected++;
                    break;
                case FeedbackType.Ignored:
                    ruleEffectiveness.IssuesIgnored++;
                    break;
            }

            ruleEffectiveness.EffectivenessScore = CalculateEffectivenessScore(ruleEffectiveness);
            ruleEffectiveness.LastUpdated = DateTime.UtcNow;

            await SaveLearningDataAsync();
            _logger.LogInformation("Updated rule effectiveness for {RuleId}: {Score}", ruleId, ruleEffectiveness.EffectivenessScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update rule effectiveness for {RuleId}", ruleId);
        }
    }

    public async Task<List<RuleEffectiveness>> GetRuleEffectivenessAsync()
    {
        return _learningData
            .SelectMany(ld => ld.RuleEffectiveness)
            .GroupBy(re => re.RuleId)
            .Select(g => new RuleEffectiveness
            {
                RuleId = g.Key,
                IssuesFound = g.Sum(x => x.IssuesFound),
                IssuesAccepted = g.Sum(x => x.IssuesAccepted),
                IssuesRejected = g.Sum(x => x.IssuesRejected),
                IssuesIgnored = g.Sum(x => x.IssuesIgnored),
                EffectivenessScore = g.Average(x => x.EffectivenessScore),
                LastUpdated = g.Max(x => x.LastUpdated)
            })
            .OrderByDescending(re => re.EffectivenessScore)
            .ToList();
        await Task.CompletedTask;
    }

    public async Task<double> CalculateRuleConfidenceAsync(string ruleId)
    {
        var ruleData = _learningData
            .SelectMany(ld => ld.RuleEffectiveness)
            .Where(re => re.RuleId == ruleId)
            .ToList();

        if (!ruleData.Any())
            return 0.5; // Default confidence for new rules

        var totalIssues = ruleData.Sum(rd => rd.IssuesFound);
        var acceptedIssues = ruleData.Sum(rd => rd.IssuesAccepted);
        var rejectedIssues = ruleData.Sum(rd => rd.IssuesRejected);

        if (totalIssues == 0)
            return 0.5;

        // Calculate confidence based on acceptance rate and total usage
        var acceptanceRate = (double)acceptedIssues / totalIssues;
        var rejectionRate = (double)rejectedIssues / totalIssues;
        var confidence = Math.Max(0.1, Math.Min(0.95, acceptanceRate - (rejectionRate * 0.5)));

        return confidence;
    }

    public async Task<List<CodeIssue>> FilterIssuesByRelevanceAsync(List<CodeIssue> issues, string repositoryName)
    {
        var filteredIssues = new List<CodeIssue>();
        var ruleEffectiveness = await GetRuleEffectivenessAsync();
        var effectivenessDict = ruleEffectiveness.ToDictionary(re => re.RuleId, re => re.EffectivenessScore);

        foreach (var issue in issues)
        {
            var effectiveness = effectivenessDict.GetValueOrDefault(issue.RuleId, 0.5);
            var confidence = await CalculateRuleConfidenceAsync(issue.RuleId);

            // Filter based on effectiveness and confidence thresholds
            if (effectiveness >= 0.3 && confidence >= 0.4)
            {
                // Apply severity-based filtering
                var severityMultiplier = issue.Severity.ToLower() switch
                {
                    "error" => 1.0,
                    "warning" => 0.8,
                    "info" => 0.6,
                    _ => 0.5
                };

                var relevanceScore = effectiveness * confidence * severityMultiplier;

                if (relevanceScore >= 0.2) // Minimum relevance threshold
                {
                    filteredIssues.Add(issue);
                }
            }
        }

        // Sort by relevance (effectiveness * confidence * severity)
        return filteredIssues
            .OrderByDescending(issue =>
            {
                var effectiveness = effectivenessDict.GetValueOrDefault(issue.RuleId, 0.5);
                var confidence = CalculateRuleConfidenceAsync(issue.RuleId).Result;
                var severityMultiplier = issue.Severity.ToLower() switch
                {
                    "error" => 1.0,
                    "warning" => 0.8,
                    "info" => 0.6,
                    _ => 0.5
                };
                return effectiveness * confidence * severityMultiplier;
            })
            .ToList();
    }

    public async Task<LearningInsights> GetLearningInsightsAsync()
    {
        var insights = new LearningInsights();

        if (!_learningData.Any())
            return insights;

        insights.TotalPullRequestsAnalyzed = _learningData.Count;
        insights.TotalIssuesFound = _learningData.Sum(ld => ld.Metrics.TotalIssuesFound);
        insights.AverageIssuesPerPR = (double)insights.TotalIssuesFound / insights.TotalPullRequestsAnalyzed;

        var ruleEffectiveness = await GetRuleEffectivenessAsync();
        insights.AverageRuleEffectiveness = ruleEffectiveness.Any() ? ruleEffectiveness.Average(re => re.EffectivenessScore) : 0;

        insights.MostEffectiveRules = ruleEffectiveness
            .Where(re => re.EffectivenessScore >= 0.7)
            .OrderByDescending(re => re.EffectivenessScore)
            .Take(5)
            .Select(re => re.RuleId)
            .ToList();

        insights.LeastEffectiveRules = ruleEffectiveness
            .Where(re => re.EffectivenessScore < 0.3)
            .OrderBy(re => re.EffectivenessScore)
            .Take(5)
            .Select(re => re.RuleId)
            .ToList();

        insights.RuleEffectivenessScores = ruleEffectiveness
            .ToDictionary(re => re.RuleId, re => re.EffectivenessScore);

        // Calculate developer satisfaction based on acceptance rate
        var totalAccepted = _learningData.Sum(ld => ld.Metrics.CommentsAccepted);
        var totalRejected = _learningData.Sum(ld => ld.Metrics.CommentsRejected);
        var totalComments = totalAccepted + totalRejected;

        insights.DeveloperSatisfactionScore = totalComments > 0 ? (double)totalAccepted / totalComments : 0.5;

        // Generate recommendations
        insights.RecommendedRuleAdjustments = GenerateRuleRecommendations(ruleEffectiveness);

        return insights;
    }

    public async Task OptimizeRulesAsync()
    {
        try
        {
            var ruleEffectiveness = await GetRuleEffectivenessAsync();
            var lowPerformingRules = ruleEffectiveness
                .Where(re => re.EffectivenessScore < 0.2 && re.IssuesFound > 10)
                .ToList();

            foreach (var rule in lowPerformingRules)
            {
                _logger.LogInformation("Disabling low-performing rule: {RuleId} (Effectiveness: {Score})",
                    rule.RuleId, rule.EffectivenessScore);

                // In a real implementation, you would disable or adjust the rule
                // For now, we'll just log it
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize rules");
        }
    }

    private async Task LoadLearningDataAsync()
    {
        try
        {
            if (File.Exists(_learningDataPath))
            {
                var json = await File.ReadAllTextAsync(_learningDataPath);
                _learningData = JsonSerializer.Deserialize<List<LearningData>>(json) ?? new List<LearningData>();
                _logger.LogInformation("Loaded {Count} learning data entries", _learningData.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load learning data");
            _learningData = new List<LearningData>();
        }
    }

    private async Task LoadAdaptiveRulesAsync()
    {
        try
        {
            if (File.Exists(_adaptiveRulesPath))
            {
                var json = await File.ReadAllTextAsync(_adaptiveRulesPath);
                _adaptiveRules = JsonSerializer.Deserialize<List<AdaptiveRule>>(json) ?? new List<AdaptiveRule>();
            }
            else
            {
                _adaptiveRules = new List<AdaptiveRule>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load adaptive rules");
            _adaptiveRules = new List<AdaptiveRule>();
        }
    }

    private async Task SaveLearningDataAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_learningData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_learningDataPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save learning data");
        }
    }

    private async Task UpdateRuleEffectivenessFromDataAsync(LearningData learningData)
    {
        foreach (var issue in learningData.Metrics.RuleUsageCount)
        {
            var ruleEffectiveness = learningData.RuleEffectiveness
                .FirstOrDefault(re => re.RuleId == issue.Key);

            if (ruleEffectiveness == null)
            {
                ruleEffectiveness = new RuleEffectiveness
                {
                    RuleId = issue.Key,
                    IssuesFound = issue.Value,
                    LastUpdated = DateTime.UtcNow
                };
                learningData.RuleEffectiveness.Add(ruleEffectiveness);
            }
            else
            {
                ruleEffectiveness.IssuesFound += issue.Value;
            }

            ruleEffectiveness.EffectivenessScore = CalculateEffectivenessScore(ruleEffectiveness);
        }

        await Task.CompletedTask;
    }

    private double CalculateEffectivenessScore(RuleEffectiveness ruleEffectiveness)
    {
        var totalIssues = ruleEffectiveness.IssuesFound;
        if (totalIssues == 0)
            return 0.5;

        var acceptedRate = (double)ruleEffectiveness.IssuesAccepted / totalIssues;
        var rejectedRate = (double)ruleEffectiveness.IssuesRejected / totalIssues;
        var ignoredRate = (double)ruleEffectiveness.IssuesIgnored / totalIssues;

        // Weight accepted issues more heavily than rejected ones
        var score = (acceptedRate * 1.0) - (rejectedRate * 0.5) - (ignoredRate * 0.2);
        return Math.Max(0.0, Math.Min(1.0, score));
    }

    private List<string> GenerateRuleRecommendations(List<RuleEffectiveness> ruleEffectiveness)
    {
        var recommendations = new List<string>();

        var lowPerformingRules = ruleEffectiveness
            .Where(re => re.EffectivenessScore < 0.3 && re.IssuesFound > 5)
            .ToList();

        foreach (var rule in lowPerformingRules)
        {
            if (rule.IssuesRejected > rule.IssuesAccepted)
            {
                recommendations.Add($"Consider adjusting rule '{rule.RuleId}' - high rejection rate ({rule.EffectivenessScore:P0})");
            }
            else if (rule.IssuesIgnored > rule.IssuesAccepted)
            {
                recommendations.Add($"Rule '{rule.RuleId}' is frequently ignored - consider improving message clarity");
            }
        }

        return recommendations;
    }
}
