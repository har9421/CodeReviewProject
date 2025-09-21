using System.Text.Json.Serialization;

namespace CodeReviewBot.Models;

/// <summary>
/// Webhook message details
/// </summary>
public class WebhookMessage
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("markdown")]
    public string? Markdown { get; set; }
}
