using System.Text.Json.Serialization;

namespace CodeReviewBot.Models;

/// <summary>
/// Repository information from webhook
/// </summary>
public class WebhookRepository
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("project")]
    public WebhookProject? Project { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
