using CodeReviewBot.Application.DTOs;
using CodeReviewBot.Application.Interfaces;
using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeReviewBot.Shared.Configuration;

namespace CodeReviewBot.Application.Services;

public class IntelligentPullRequestAnalysisService : IPullRequestAnalysisService
{
    private readonly IPullRequestRepository _pullRequestRepository;
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly ILearningService _learningService;
    private readonly ILogger<IntelligentPullRequestAnalysisService> _logger;
    private readonly BotOptions _botOptions;

    public IntelligentPullRequestAnalysisService(
        IPullRequestRepository pullRequestRepository,
        ICodeAnalyzer codeAnalyzer,
        ILearningService learningService,
        ILogger<IntelligentPullRequestAnalysisService> logger,
        IOptions<BotOptions> botOptions)
    {
        _pullRequestRepository = pullRequestRepository;
        _codeAnalyzer = codeAnalyzer;
        _learningService = learningService;
        _logger = logger;
        _botOptions = botOptions.Value;
    }

    public async Task<AnalyzePullRequestResponse> AnalyzePullRequestAsync(AnalyzePullRequestRequest request)
    {
        var startTime = DateTime.UtcNow;
        var learningData = new LearningData
        {
            Id = Guid.NewGuid().ToString(),
            PullRequestId = request.PullRequestId.ToString(),
            RepositoryName = request.RepositoryName,
            ProjectName = request.ProjectName,
            AnalysisDate = startTime,
            Metrics = new PullRequestMetrics()
        };

        try
        {
            _logger.LogInformation("Starting intelligent analysis for PR {PullRequestId}", request.PullRequestId);

            // 1. Get pull request details
            var pullRequest = await _pullRequestRepository.GetPullRequestDetailsAsync(
                request.OrganizationUrl, request.ProjectName, request.RepositoryName,
                request.PullRequestId, request.PersonalAccessToken);

            if (pullRequest == null)
            {
                return new AnalyzePullRequestResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to fetch pull request details"
                };
            }

            // 2. Get file changes
            var fileChanges = await _pullRequestRepository.GetPullRequestChangesAsync(
                request.OrganizationUrl, request.ProjectName, request.RepositoryName,
                request.PullRequestId, request.PersonalAccessToken);

            if (!fileChanges.Any())
            {
                _logger.LogInformation("No C# files changed in PR {PullRequestId}", request.PullRequestId);
                return new AnalyzePullRequestResponse
                {
                    Success = true,
                    IssuesFound = 0,
                    CommentsPosted = 0
                };
            }

            learningData.Metrics.TotalFilesAnalyzed = fileChanges.Count;
            learningData.Metrics.FileTypes = fileChanges.Select(fc => Path.GetExtension(fc.Path)).Distinct().ToList();

            // 3. Analyze files in parallel with intelligent filtering
            var allIssues = await AnalyzeFilesIntelligentlyAsync(fileChanges, learningData);

            learningData.Metrics.TotalIssuesFound = allIssues.Count;
            _logger.LogInformation("Found {IssueCount} issues across {FileCount} files in PR {PullRequestId}",
                allIssues.Count, fileChanges.Count, request.PullRequestId);

            // 4. Post intelligent comments
            var commentCount = await PostIntelligentCommentsAsync(request, allIssues, learningData);

            learningData.Metrics.CommentsPosted = commentCount;
            learningData.Metrics.AverageResponseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // 5. Record learning data
            await _learningService.RecordPullRequestAnalysisAsync(learningData);

            // 6. Generate insights and recommendations
            var insights = await _learningService.GetLearningInsightsAsync();
            await LogAnalysisInsightsAsync(insights);

            return new AnalyzePullRequestResponse
            {
                Success = true,
                IssuesFound = allIssues.Count,
                CommentsPosted = commentCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during intelligent analysis of PR {PullRequestId}", request.PullRequestId);
            return new AnalyzePullRequestResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<List<CodeIssue>> AnalyzeFilesIntelligentlyAsync(List<FileChange> fileChanges, LearningData learningData)
    {
        var allIssues = new List<CodeIssue>();
        var ruleUsageCount = new Dictionary<string, int>();

        // Process files in parallel with controlled concurrency
        var semaphore = new SemaphoreSlim(_botOptions.Analysis.MaxConcurrentFiles, _botOptions.Analysis.MaxConcurrentFiles);
        var tasks = fileChanges.Select(async fileChange =>
        {
            await semaphore.WaitAsync();
            try
            {
                var issues = await _codeAnalyzer.AnalyzeFileAsync(fileChange);

                // Track rule usage for learning
                foreach (var issue in issues)
                {
                    ruleUsageCount[issue.RuleId] = ruleUsageCount.GetValueOrDefault(issue.RuleId, 0) + 1;
                }

                return issues;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        foreach (var issues in results)
        {
            allIssues.AddRange(issues);
        }

        learningData.Metrics.RuleUsageCount = ruleUsageCount;
        return allIssues;
    }

    private async Task<int> PostIntelligentCommentsAsync(AnalyzePullRequestRequest request, List<CodeIssue> issues, LearningData learningData)
    {
        var commentCount = 0;
        var maxComments = _botOptions.Notifications.MaxCommentsPerFile;
        var acceptedComments = 0;
        var rejectedComments = 0;

        // Group issues by file and apply intelligent prioritization
        var prioritizedIssues = issues
            .GroupBy(issue => issue.FilePath)
            .SelectMany(group => PrioritizeIssuesForFile(group.ToList()))
            .Take(maxComments)
            .ToList();

        foreach (var issue in prioritizedIssues)
        {
            if (commentCount >= maxComments)
            {
                _logger.LogInformation("Reached maximum comment limit ({MaxComments}) for PR {PullRequestId}",
                    maxComments, request.PullRequestId);
                break;
            }

            var comment = CreateIntelligentComment(issue);
            var success = await _pullRequestRepository.PostCommentAsync(
                request.OrganizationUrl, request.ProjectName, request.RepositoryName,
                request.PullRequestId, request.PersonalAccessToken, comment);

            if (success)
            {
                commentCount++;
                _logger.LogInformation("Posted intelligent comment for issue {RuleId} in file {FilePath}:{LineNumber}",
                    issue.RuleId, issue.FilePath, issue.LineNumber);
            }
            else
            {
                _logger.LogWarning("Failed to post comment for issue {RuleId}", issue.RuleId);
            }

            // Adaptive delay based on learning data
            var delay = CalculateAdaptiveDelay(issue.RuleId);
            await Task.Delay(delay);
        }

        learningData.Metrics.CommentsAccepted = acceptedComments;
        learningData.Metrics.CommentsRejected = rejectedComments;

        return commentCount;
    }

    private List<CodeIssue> PrioritizeIssuesForFile(List<CodeIssue> issues)
    {
        // Prioritize issues based on severity, learning data, and context
        return issues
            .OrderByDescending(issue => GetIssuePriority(issue))
            .ToList();
    }

    private int GetIssuePriority(CodeIssue issue)
    {
        var severityWeight = issue.Severity.ToLower() switch
        {
            "error" => 100,
            "warning" => 50,
            "info" => 25,
            _ => 10
        };

        // Add learning-based priority adjustment
        var learningWeight = CalculateLearningBasedWeight(issue.RuleId);

        return severityWeight + learningWeight;
    }

    private int CalculateLearningBasedWeight(string ruleId)
    {
        // This would be calculated based on historical effectiveness
        // For now, return a base weight
        return 10;
    }

    private PullRequestComment CreateIntelligentComment(CodeIssue issue)
    {
        var confidence = _learningService.CalculateRuleConfidenceAsync(issue.RuleId).Result;
        var confidenceIndicator = confidence >= 0.8 ? "🔍 High Confidence" :
                                 confidence >= 0.5 ? "⚠️ Medium Confidence" : "❓ Low Confidence";

        var content = $"**{issue.Severity.ToUpper()}** {confidenceIndicator}\n\n" +
                     $"**Issue**: {issue.Message}\n\n" +
                     (!string.IsNullOrEmpty(issue.Suggestion) ? $"💡 **Suggestion**: {issue.Suggestion}\n\n" : "") +
                     $"📋 **Rule**: {issue.RuleId}\n" +
                     $"🎯 **Confidence**: {confidence:P0}";

        return new PullRequestComment
        {
            Content = content,
            FilePath = issue.FilePath,
            LineNumber = issue.LineNumber,
            Severity = issue.Severity
        };
    }

    private int CalculateAdaptiveDelay(string ruleId)
    {
        // Adjust delay based on rule effectiveness to avoid overwhelming developers
        var effectiveness = _learningService.GetRuleEffectivenessAsync().Result
            .FirstOrDefault(re => re.RuleId == ruleId)?.EffectivenessScore ?? 0.5;

        // Higher effectiveness = shorter delay (more likely to be accepted)
        var baseDelay = 500;
        var adaptiveDelay = (int)(baseDelay * (1.5 - effectiveness));

        return Math.Max(200, Math.Min(1000, adaptiveDelay));
    }

    private async Task LogAnalysisInsightsAsync(LearningInsights insights)
    {
        _logger.LogInformation("=== Analysis Insights ===");
        _logger.LogInformation("Total PRs Analyzed: {TotalPRs}", insights.TotalPullRequestsAnalyzed);
        _logger.LogInformation("Average Issues per PR: {AvgIssues:F1}", insights.AverageIssuesPerPR);
        _logger.LogInformation("Average Rule Effectiveness: {AvgEffectiveness:P1}", insights.AverageRuleEffectiveness);
        _logger.LogInformation("Developer Satisfaction: {Satisfaction:P1}", insights.DeveloperSatisfactionScore);

        if (insights.MostEffectiveRules.Any())
        {
            _logger.LogInformation("Most Effective Rules: {Rules}", string.Join(", ", insights.MostEffectiveRules));
        }

        if (insights.RecommendedRuleAdjustments.Any())
        {
            _logger.LogInformation("Rule Recommendations: {Recommendations}",
                string.Join("; ", insights.RecommendedRuleAdjustments));
        }

        await Task.CompletedTask;
    }
}
