namespace CodeReviewBot.Configuration;

public class MetricsOptions
{
    public const string SectionName = "Metrics";

    public bool Enabled { get; set; } = true;
    public bool StoreHistoricalData { get; set; } = true;
    public int RetentionDays { get; set; } = 365;
    public bool GenerateReports { get; set; } = true;
    public TimeSpan ReportGenerationInterval { get; set; } = TimeSpan.FromDays(7);
}
