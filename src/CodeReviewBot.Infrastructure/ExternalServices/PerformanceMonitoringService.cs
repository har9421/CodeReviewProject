using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CodeReviewBot.Infrastructure.ExternalServices;

public class PerformanceMonitoringService : IPerformanceMonitoringService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();
    private readonly ConcurrentQueue<PerformanceEvent> _events = new();
    private readonly Timer _cleanupTimer;

    public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupOldMetrics, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public void RecordAnalysisTime(string operation, TimeSpan duration, int itemsProcessed)
    {
        var metrics = _metrics.GetOrAdd(operation, _ => new PerformanceMetrics
        {
            OperationName = operation,
            StartTime = DateTime.UtcNow
        });

        lock (metrics)
        {
            metrics.TotalExecutions++;
            metrics.TotalDuration += duration;
            metrics.TotalItemsProcessed += itemsProcessed;
            metrics.AverageDuration = TimeSpan.FromTicks(metrics.TotalDuration.Ticks / metrics.TotalExecutions);
            metrics.ItemsPerSecond = metrics.TotalItemsProcessed / metrics.TotalDuration.TotalSeconds;
            metrics.LastExecution = DateTime.UtcNow;
        }

        _events.Enqueue(new PerformanceEvent
        {
            OperationName = operation,
            Duration = duration,
            ItemsProcessed = itemsProcessed,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogDebug("Recorded performance for {Operation}: {Duration}ms for {Items} items ({Rate:F2} items/sec)",
            operation, duration.TotalMilliseconds, itemsProcessed, metrics.ItemsPerSecond);
    }

    public void RecordMemoryUsage(string operation, long memoryBytes)
    {
        var metrics = _metrics.GetOrAdd(operation, _ => new PerformanceMetrics
        {
            OperationName = operation,
            StartTime = DateTime.UtcNow
        });

        lock (metrics)
        {
            metrics.PeakMemoryUsage = Math.Max(metrics.PeakMemoryUsage, memoryBytes);
            metrics.CurrentMemoryUsage = memoryBytes;
        }
    }

    public void RecordError(string operation, Exception exception)
    {
        var metrics = _metrics.GetOrAdd(operation, _ => new PerformanceMetrics
        {
            OperationName = operation,
            StartTime = DateTime.UtcNow
        });

        lock (metrics)
        {
            metrics.ErrorCount++;
            metrics.LastError = exception.Message;
            metrics.LastErrorTime = DateTime.UtcNow;
        }

        _logger.LogError(exception, "Error recorded for operation {Operation}", operation);
    }

    public PerformanceReport GetPerformanceReport()
    {
        var report = new PerformanceReport
        {
            GeneratedAt = DateTime.UtcNow,
            Metrics = _metrics.Values.ToList(),
            RecentEvents = _events.TakeLast(100).ToList(),
            Summary = GenerateSummary()
        };

        return report;
    }

    public List<PerformanceAlert> GetPerformanceAlerts()
    {
        var alerts = new List<PerformanceAlert>();

        foreach (var metric in _metrics.Values)
        {
            // Alert on high error rate
            if (metric.TotalExecutions > 10 && (double)metric.ErrorCount / metric.TotalExecutions > 0.1)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighErrorRate,
                    Operation = metric.OperationName,
                    Message = $"High error rate: {metric.ErrorCount}/{metric.TotalExecutions} executions failed",
                    Severity = AlertSeverity.Warning,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Alert on slow performance
            if (metric.AverageDuration.TotalSeconds > 30)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.SlowPerformance,
                    Operation = metric.OperationName,
                    Message = $"Slow performance: Average duration {metric.AverageDuration.TotalSeconds:F1}s",
                    Severity = AlertSeverity.Warning,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Alert on high memory usage
            if (metric.PeakMemoryUsage > 100 * 1024 * 1024) // 100MB
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighMemoryUsage,
                    Operation = metric.OperationName,
                    Message = $"High memory usage: {metric.PeakMemoryUsage / (1024 * 1024)}MB",
                    Severity = AlertSeverity.Warning,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        return alerts;
    }

    public void ResetMetrics()
    {
        _metrics.Clear();
        while (_events.TryDequeue(out _)) { }
        _logger.LogInformation("Performance metrics reset");
    }

    private PerformanceSummary GenerateSummary()
    {
        var totalExecutions = _metrics.Values.Sum(m => m.TotalExecutions);
        var totalDuration = TimeSpan.FromTicks(_metrics.Values.Sum(m => m.TotalDuration.Ticks));
        var totalErrors = _metrics.Values.Sum(m => m.ErrorCount);
        var totalItems = _metrics.Values.Sum(m => m.TotalItemsProcessed);

        return new PerformanceSummary
        {
            TotalExecutions = totalExecutions,
            TotalDuration = totalDuration,
            AverageDuration = totalExecutions > 0 ? TimeSpan.FromTicks(totalDuration.Ticks / totalExecutions) : TimeSpan.Zero,
            TotalErrors = totalErrors,
            ErrorRate = totalExecutions > 0 ? (double)totalErrors / totalExecutions : 0,
            TotalItemsProcessed = totalItems,
            OverallThroughput = totalDuration.TotalSeconds > 0 ? totalItems / totalDuration.TotalSeconds : 0
        };
    }

    private void CleanupOldMetrics(object? state)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);

        // Remove old events
        while (_events.TryPeek(out var oldestEvent) && oldestEvent.Timestamp < cutoff)
        {
            _events.TryDequeue(out _);
        }

        _logger.LogDebug("Cleaned up old performance metrics");
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}
