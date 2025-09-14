using System.ComponentModel.DataAnnotations;

namespace CodeReviewRunner.Configuration;

public class CodeReviewOptions
{
    public const string SectionName = "CodeReview";

    [Required]
    public AzureDevOpsOptions AzureDevOps { get; set; } = new();

    [Required]
    public AnalysisOptions Analysis { get; set; } = new();

    [Required]
    public RulesOptions Rules { get; set; } = new();

    [Required]
    public NotificationsOptions Notifications { get; set; } = new();
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

    public string[] SupportedFileExtensions { get; set; } = { ".cs", ".js", ".jsx", ".ts", ".tsx" };

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
