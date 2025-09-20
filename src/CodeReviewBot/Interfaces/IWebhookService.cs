using CodeReviewBot.Models;

namespace CodeReviewBot.Interfaces;

/// <summary>
/// Service for handling Azure DevOps webhooks
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Processes an Azure DevOps webhook
    /// </summary>
    Task<WebhookProcessingResult> ProcessWebhookAsync(AzureDevOpsWebhook webhook);

    /// <summary>
    /// Configures the bot for a specific organization/project
    /// </summary>
    Task<BotConfigurationResult> ConfigureBotAsync(BotConfigurationRequest request);

    /// <summary>
    /// Validates webhook signature and authenticity
    /// </summary>
    bool ValidateWebhookSignature(string payload, string signature);

    /// <summary>
    /// Gets bot configuration for an organization
    /// </summary>
    Task<BotConfigurationRequest?> GetConfigurationAsync(string organization, string project);
}
