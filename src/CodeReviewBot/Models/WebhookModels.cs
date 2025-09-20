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

/// <summary>
/// User information from webhook
/// </summary>
public class WebhookUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("uniqueName")]
    public string UniqueName { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}

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

/// <summary>
/// Bot configuration request
/// </summary>
public class BotConfigurationRequest
{
    [JsonPropertyName("organization")]
    public string Organization { get; set; } = string.Empty;

    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("rulesUrl")]
    public string RulesUrl { get; set; } = string.Empty;

    [JsonPropertyName("aiEnabled")]
    public bool AIEnabled { get; set; } = false;

    [JsonPropertyName("aiApiKey")]
    public string? AIApiKey { get; set; }

    [JsonPropertyName("aiModel")]
    public string AIModel { get; set; } = "gpt-4";

    [JsonPropertyName("learningEnabled")]
    public bool LearningEnabled { get; set; } = true;

    [JsonPropertyName("maxCommentsPerFile")]
    public int MaxCommentsPerFile { get; set; } = 50;

    [JsonPropertyName("enableSummary")]
    public bool EnableSummary { get; set; } = true;

    [JsonPropertyName("severityThreshold")]
    public string SeverityThreshold { get; set; } = "warning";

    [JsonPropertyName("webhookUrl")]
    public string WebhookUrl { get; set; } = string.Empty;
}

/// <summary>
/// Webhook processing result
/// </summary>
public class WebhookProcessingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AnalysisId { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public int IssuesFound { get; set; }
    public int CommentsPosted { get; set; }
}

/// <summary>
/// Bot configuration result
/// </summary>
public class BotConfigurationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ConfigurationId { get; set; }
    public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
}
