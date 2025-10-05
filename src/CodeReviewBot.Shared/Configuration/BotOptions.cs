using System.ComponentModel.DataAnnotations;

namespace CodeReviewBot.Shared.Configuration;

public class BotOptions
{
    public const string SectionName = "Bot";

    [Required]
    public string Name { get; set; } = "Intelligent C# Code Review Bot";

    [Required]
    public string Version { get; set; } = "1.0.0";

    public string DefaultRulesUrl { get; set; } = "coding-standards.json";

    [Required]
    public WebhookOptions Webhook { get; set; } = new();

    public AzureDevOpsOptions? AzureDevOps { get; set; }

    [Required]
    public AnalysisOptions Analysis { get; set; } = new();

    [Required]
    public NotificationsOptions Notifications { get; set; } = new();

    public LearningOptions? Learning { get; set; }

    public PerformanceOptions? Performance { get; set; }
}

public class WebhookOptions
{
    public const string SectionName = "Webhook";

    public string Secret { get; set; } = "";

    public List<string> AllowedEvents { get; set; } = new() { "git.pullrequest.created", "git.pullrequest.updated" };

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

public class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://dev.azure.com";

    [Required]
    public string ApiVersion { get; set; } = "7.0";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(0, 10)]
    public int RetryAttempts { get; set; } = 3;

    [Range(1, 60)]
    public int RetryDelaySeconds { get; set; } = 2;
}

public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    [Range(1, 50)]
    public int MaxConcurrentFiles { get; set; } = 10;

    [Range(1, 1440)]
    public int CacheRulesMinutes { get; set; } = 60;

    public bool EnableCaching { get; set; } = true;

    public List<string> SupportedFileExtensions { get; set; } = new() { ".cs" };

    [Range(1, 10000)]
    public int MaxFileSizeKB { get; set; } = 1024;

    public bool EnableIntelligentFiltering { get; set; } = true;

    [Range(0.0, 1.0)]
    public double MinConfidenceThreshold { get; set; } = 0.3;

    public bool EnableParallelProcessing { get; set; } = true;
}

public class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public bool EnableComments { get; set; } = true;

    public bool EnableSummary { get; set; } = true;

    [Range(1, 100)]
    public int MaxCommentsPerFile { get; set; } = 50;

    public bool EnableConfidenceIndicators { get; set; } = true;

    public bool AdaptiveDelayEnabled { get; set; } = true;
}

public class LearningOptions
{
    public const string SectionName = "Learning";

    public bool EnableLearning { get; set; } = true;

    [Range(1, 365)]
    public int DataRetentionDays { get; set; } = 90;

    public bool AutoOptimizeRules { get; set; } = true;

    public bool FeedbackCollectionEnabled { get; set; } = true;

    [Range(1, 100)]
    public int MinDataPointsForOptimization { get; set; } = 10;
}

public class PerformanceOptions
{
    public const string SectionName = "Performance";

    public bool EnableMonitoring { get; set; } = true;

    [Range(1, 168)]
    public int MetricsRetentionHours { get; set; } = 24;

    public PerformanceAlertThresholds AlertThresholds { get; set; } = new();
}

public class PerformanceAlertThresholds
{
    [Range(0.0, 1.0)]
    public double HighErrorRate { get; set; } = 0.1;

    [Range(1, 300)]
    public int SlowPerformanceSeconds { get; set; } = 30;

    [Range(1, 10000)]
    public int HighMemoryUsageMB { get; set; } = 100;
}
