using FluentValidation;

namespace CodeReviewBot.Domain.Entities;

public class LearningData
{
    public string Id { get; set; } = string.Empty;
    public string PullRequestId { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public List<RuleEffectiveness> RuleEffectiveness { get; set; } = new();
    public List<DeveloperFeedback> DeveloperFeedback { get; set; } = new();
    public PullRequestMetrics Metrics { get; set; } = new();
}

public class RuleEffectiveness
{
    public string RuleId { get; set; } = string.Empty;
    public int IssuesFound { get; set; }
    public int IssuesAccepted { get; set; }
    public int IssuesRejected { get; set; }
    public int IssuesIgnored { get; set; }
    public double EffectivenessScore { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class DeveloperFeedback
{
    public string IssueId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public FeedbackType Type { get; set; }
    public string? Comment { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum FeedbackType
{
    Accepted,
    Rejected,
    Ignored,
    FalsePositive,
    TruePositive
}

public class PullRequestMetrics
{
    public int TotalFilesAnalyzed { get; set; }
    public int TotalIssuesFound { get; set; }
    public int CommentsPosted { get; set; }
    public int CommentsAccepted { get; set; }
    public int CommentsRejected { get; set; }
    public double AverageResponseTime { get; set; }
    public List<string> FileTypes { get; set; } = new();
    public Dictionary<string, int> RuleUsageCount { get; set; } = new();
}

public class AdaptiveRule
{
    public string Id { get; set; } = string.Empty;
    public string OriginalRuleId { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
    public double ConfidenceScore { get; set; }
    public int UsageCount { get; set; }
    public double EffectivenessScore { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> ContextPatterns { get; set; } = new();
}

public class LearningDataValidator : AbstractValidator<LearningData>
{
    public LearningDataValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PullRequestId).NotEmpty();
        RuleFor(x => x.RepositoryName).NotEmpty();
        RuleFor(x => x.ProjectName).NotEmpty();
        RuleFor(x => x.AnalysisDate).NotEmpty();
    }
}

public class RuleEffectivenessValidator : AbstractValidator<RuleEffectiveness>
{
    public RuleEffectivenessValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.EffectivenessScore).InclusiveBetween(0, 1);
    }
}

public class DeveloperFeedbackValidator : AbstractValidator<DeveloperFeedback>
{
    public DeveloperFeedbackValidator()
    {
        RuleFor(x => x.IssueId).NotEmpty();
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.FilePath).NotEmpty();
        RuleFor(x => x.LineNumber).GreaterThan(0);
    }
}
