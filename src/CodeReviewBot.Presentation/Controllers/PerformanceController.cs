using CodeReviewBot.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CodeReviewBot.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerformanceController : ControllerBase
{
    private readonly IPerformanceMonitoringService _performanceService;
    private readonly ILogger<PerformanceController> _logger;

    public PerformanceController(IPerformanceMonitoringService performanceService, ILogger<PerformanceController> logger)
    {
        _performanceService = performanceService;
        _logger = logger;
    }

    [HttpGet("report")]
    public IActionResult GetPerformanceReport()
    {
        try
        {
            var report = _performanceService.GetPerformanceReport();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance report");
            return StatusCode(500, new { error = "Failed to retrieve performance report" });
        }
    }

    [HttpGet("alerts")]
    public IActionResult GetPerformanceAlerts()
    {
        try
        {
            var alerts = _performanceService.GetPerformanceAlerts();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance alerts");
            return StatusCode(500, new { error = "Failed to retrieve performance alerts" });
        }
    }

    [HttpPost("reset")]
    public IActionResult ResetMetrics()
    {
        try
        {
            _performanceService.ResetMetrics();
            _logger.LogInformation("Performance metrics reset requested");
            return Ok(new { message = "Performance metrics reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting performance metrics");
            return StatusCode(500, new { error = "Failed to reset performance metrics" });
        }
    }

    [HttpGet("health")]
    public IActionResult GetHealthStatus()
    {
        try
        {
            var alerts = _performanceService.GetPerformanceAlerts();
            var criticalAlerts = alerts.Count(a => a.Severity == AlertSeverity.Critical);
            var warningAlerts = alerts.Count(a => a.Severity == AlertSeverity.Warning);

            var status = criticalAlerts > 0 ? "unhealthy" :
                        warningAlerts > 5 ? "degraded" : "healthy";

            return Ok(new
            {
                status,
                criticalAlerts,
                warningAlerts,
                totalAlerts = alerts.Count,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health status");
            return StatusCode(500, new { error = "Failed to retrieve health status" });
        }
    }
}
