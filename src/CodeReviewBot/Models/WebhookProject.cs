using System.Text.Json.Serialization;

namespace CodeReviewBot.Models;

/// <summary>
/// Project information from webhook
/// </summary>
public class WebhookProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
