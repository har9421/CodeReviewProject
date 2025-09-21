using System.ComponentModel.DataAnnotations;

namespace CodeReviewBot.Configuration;

public class RulesOptions
{
    [Range(1, 1440)]
    public int CacheTimeoutMinutes { get; set; } = 60;

    public bool ValidationEnabled { get; set; } = true;
}
