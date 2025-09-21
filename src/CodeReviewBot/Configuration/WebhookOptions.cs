using System.ComponentModel.DataAnnotations;

namespace CodeReviewBot.Configuration;

public class WebhookOptions
{
    [Required]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public List<string> AllowedEvents { get; set; } = new() { "git.pullrequest.created", "git.pullrequest.updated" };

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
