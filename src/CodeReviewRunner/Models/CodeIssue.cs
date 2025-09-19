using System.Text.Json.Serialization;

namespace CodeReviewRunner.Models;

public class CodeIssue
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "warning";

    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("analyzer")]
    public string Analyzer { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 1.0;

    [JsonPropertyName("lineText")]
    public string? LineText { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}