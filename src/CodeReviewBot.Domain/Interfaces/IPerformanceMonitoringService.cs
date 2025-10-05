namespace CodeReviewBot.Domain.Interfaces;

public interface IPerformanceMonitoringService
{
    void RecordAnalysisTime(string operation, TimeSpan duration, int itemsProcessed);
    void RecordMemoryUsage(string operation, long memoryBytes);
    void RecordError(string operation, Exception exception);
    PerformanceReport GetPerformanceReport();
    List<PerformanceAlert> GetPerformanceAlerts();
    void ResetMetrics();
}

// Performance monitoring data models
public class PerformanceReport
{
    public DateTime GeneratedAt { get; set; }
    public List<PerformanceMetrics> Metrics { get; set; } = new();
    public List<PerformanceEvent> RecentEvents { get; set; } = new();
    public PerformanceSummary Summary { get; set; } = new();
}

public class PerformanceMetrics
{
    public string OperationName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime LastExecution { get; set; }
    public int TotalExecutions { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public int TotalItemsProcessed { get; set; }
    public double ItemsPerSecond { get; set; }
    public long PeakMemoryUsage { get; set; }
    public long CurrentMemoryUsage { get; set; }
    public int ErrorCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorTime { get; set; }
}

public class PerformanceEvent
{
    public string OperationName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int ItemsProcessed { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PerformanceSummary
{
    public int TotalExecutions { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public int TotalErrors { get; set; }
    public double ErrorRate { get; set; }
    public int TotalItemsProcessed { get; set; }
    public double OverallThroughput { get; set; }
}

public class PerformanceAlert
{
    public AlertType Type { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum AlertType
{
    HighErrorRate,
    SlowPerformance,
    HighMemoryUsage,
    LowThroughput
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}
