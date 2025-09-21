using System.Text.Json.Serialization;

namespace CodeReviewBot.Models;

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
