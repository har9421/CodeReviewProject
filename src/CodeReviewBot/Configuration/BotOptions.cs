using System.ComponentModel.DataAnnotations;

namespace CodeReviewBot.Configuration;

public class BotOptions
{
    public const string SectionName = "Bot";

    [Required]
    public string Name { get; set; } = "Intelligent C# Code Review Bot";

    [Required]
    public string Version { get; set; } = "1.0.0";

    [Required]
    public WebhookOptions Webhook { get; set; } = new();

    [Required]
    public AnalysisOptions Analysis { get; set; } = new();

    [Required]
    public NotificationsOptions Notifications { get; set; } = new();

    public AIOptions? AI { get; set; }

    public LearningOptions? Learning { get; set; }

    public MetricsOptions? Metrics { get; set; }
}

public class WebhookOptions
{
    [Required]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public List<string> AllowedEvents { get; set; } = new() { "git.pullrequest.created", "git.pullrequest.updated" };

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

public class AzureDevOpsOptions
{
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
    [Range(1, 100)]
    public int MaxConcurrentFiles { get; set; } = 10;

    [Range(1, 1440)]
    public int CacheRulesMinutes { get; set; } = 60;

    public bool EnableCaching { get; set; } = true;

    public string[] SupportedFileExtensions { get; set; } = { ".cs" };

    [Range(1, 10240)]
    public int MaxFileSizeKB { get; set; } = 1024;
}

public class RulesOptions
{
    [Range(1, 1440)]
    public int CacheTimeoutMinutes { get; set; } = 60;

    public bool ValidationEnabled { get; set; } = true;
}

public class NotificationsOptions
{
    public bool EnableComments { get; set; } = true;

    public bool EnableSummary { get; set; } = true;

    [Range(1, 1000)]
    public int MaxCommentsPerFile { get; set; } = 50;
}

public class AIOptions
{
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4";
    public int MaxTokens { get; set; } = 2000;
    public double Temperature { get; set; } = 0.3;
    public string SystemPrompt { get; set; } = @"
You are an expert C# code reviewer and software architect. Your role is to analyze code and provide intelligent, actionable feedback to help developers write better, more maintainable code.

Focus on:
- Code quality and maintainability
- Security best practices
- Performance optimization
- Design patterns and architectural principles
- C# and .NET best practices
- Team coding standards

Provide specific, actionable suggestions with clear explanations. Be constructive and educational in your feedback.
";
}

public class LearningOptions
{
    public bool Enabled { get; set; } = true;
    public int MinFeedbackForAdaptation { get; set; } = 5;
    public int FeedbackHistoryDays { get; set; } = 30;
    public double LowHelpfulnessThreshold { get; set; } = 0.3;
    public double HighHelpfulnessThreshold { get; set; } = 0.8;
    public bool EnablePatternLearning { get; set; } = true;
    public bool EnablePreferenceAdaptation { get; set; } = true;
}

public class MetricsOptions
{
    public bool Enabled { get; set; } = true;
    public bool StoreHistoricalData { get; set; } = true;
    public int RetentionDays { get; set; } = 365;
    public bool GenerateReports { get; set; } = true;
    public TimeSpan ReportGenerationInterval { get; set; } = TimeSpan.FromDays(7);
}
