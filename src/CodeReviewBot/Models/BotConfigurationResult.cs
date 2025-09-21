namespace CodeReviewBot.Models;

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
