namespace CodeReviewBot.Configuration;

public class LearningOptions
{
    public const string SectionName = "Learning";

    public bool Enabled { get; set; } = true;
    public int MinFeedbackForAdaptation { get; set; } = 5;
    public int FeedbackHistoryDays { get; set; } = 30;
    public double LowHelpfulnessThreshold { get; set; } = 0.3;
    public double HighHelpfulnessThreshold { get; set; } = 0.8;
    public bool EnablePatternLearning { get; set; } = true;
    public bool EnablePreferenceAdaptation { get; set; } = true;
}
