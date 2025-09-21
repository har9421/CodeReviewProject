using System.Text.Json.Serialization;

namespace CodeReviewBot.Models;

/// <summary>
/// Commit information from webhook
/// </summary>
public class WebhookCommit
{
    [JsonPropertyName("commitId")]
    public string CommitId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
