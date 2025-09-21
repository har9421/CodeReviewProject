using System.Text.Json.Serialization;

namespace CodeReviewBot.Models;

/// <summary>
/// Webhook resource containing pull request information
/// </summary>
public class WebhookResource
{
    [JsonPropertyName("repository")]
    public WebhookRepository? Repository { get; set; }

    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("sourceRefName")]
    public string SourceRefName { get; set; } = string.Empty;

    [JsonPropertyName("targetRefName")]
    public string TargetRefName { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public WebhookUser? CreatedBy { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; }

    [JsonPropertyName("lastMergeTargetCommit")]
    public WebhookCommit? LastMergeTargetCommit { get; set; }

    [JsonPropertyName("lastMergeSourceCommit")]
    public WebhookCommit? LastMergeSourceCommit { get; set; }
}
