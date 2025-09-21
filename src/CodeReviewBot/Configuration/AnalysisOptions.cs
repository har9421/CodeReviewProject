using System.ComponentModel.DataAnnotations;

namespace CodeReviewBot.Configuration;

public class AnalysisOptions
{
    [Range(1, 100)]
    public int MaxConcurrentFiles { get; set; } = 10;

    [Range(1, 1440)]
    public int CacheRulesMinutes { get; set; } = 60;

    public bool EnableCaching { get; set; } = true;

    public string[] SupportedFileExtensions { get; set; } = { ".cs" };

    [Range(1, 10240)]
    public int MaxFileSizeKB { get; set; } = 1024;
}
