using System.ComponentModel.DataAnnotations;

namespace CodeReviewRunner.Configuration;

public class ResilienceOptions
{
    public const string SectionName = "Resilience";

    [Required]
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    [Required]
    public RetryOptions Retry { get; set; } = new();
}

public class CircuitBreakerOptions
{
    [Range(1, 100)]
    public int FailureThreshold { get; set; } = 5;

    [Range(1, 300)]
    public int SamplingDurationSeconds { get; set; } = 30;

    [Range(1, 100)]
    public int MinimumThroughput { get; set; } = 2;

    [Range(1, 300)]
    public int DurationOfBreakSeconds { get; set; } = 30;
}

public class RetryOptions
{
    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 3;

    [Range(1, 60)]
    public int BaseDelaySeconds { get; set; } = 2;

    [Range(1, 300)]
    public int MaxDelaySeconds { get; set; } = 30;
}
