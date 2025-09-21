namespace CodeReviewBot.Models;

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
