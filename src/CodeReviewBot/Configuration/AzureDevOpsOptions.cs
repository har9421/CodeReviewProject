using System.ComponentModel.DataAnnotations;

namespace CodeReviewBot.Configuration;

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
