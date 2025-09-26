using System.ComponentModel.DataAnnotations;

namespace CodeReviewBot.Configuration;

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

    public AIOptions? AI { get; set; }

    public LearningOptions? Learning { get; set; }

    public MetricsOptions? Metrics { get; set; }
}