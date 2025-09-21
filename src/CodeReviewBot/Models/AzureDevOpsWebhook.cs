using System.Text.Json.Serialization;

namespace CodeReviewBot.Models;

/// <summary>
/// Azure DevOps webhook payload
/// </summary>
public class AzureDevOpsWebhook
{
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("publisherId")]
    public string PublisherId { get; set; } = string.Empty;

    [JsonPropertyName("resource")]
    public WebhookResource? Resource { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }

    [JsonPropertyName("detailedMessage")]
    public WebhookMessage? DetailedMessage { get; set; }

    [JsonPropertyName("resourceVersion")]
    public string ResourceVersion { get; set; } = string.Empty;
}
