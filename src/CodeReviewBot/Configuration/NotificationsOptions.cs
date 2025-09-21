using System.ComponentModel.DataAnnotations;

namespace CodeReviewBot.Configuration;

public class NotificationsOptions
{
    public bool EnableComments { get; set; } = true;

    public bool EnableSummary { get; set; } = true;

    [Range(1, 1000)]
    public int MaxCommentsPerFile { get; set; } = 50;
}
