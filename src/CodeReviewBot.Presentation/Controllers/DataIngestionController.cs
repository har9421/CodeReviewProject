using CodeReviewBot.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CodeReviewBot.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataIngestionController : ControllerBase
{
    private readonly GitHubDataIngestionService _ingestionService;
    private readonly ILogger<DataIngestionController> _logger;
    private static readonly Dictionary<string, IngestionProgress> _activeIngestions = new();

    public DataIngestionController(
        GitHubDataIngestionService ingestionService,
        ILogger<DataIngestionController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartIngestion([FromBody] IngestionConfig config)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate GitHub token
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (string.IsNullOrEmpty(githubToken))
            {
                return BadRequest(new { error = "GITHUB_TOKEN environment variable is required" });
            }

            _logger.LogInformation("Starting data ingestion with config: {Config}", JsonSerializer.Serialize(config));

            // Start ingestion in background
            var progress = await _ingestionService.StartBulkIngestionAsync(config);
            _activeIngestions[progress.Id] = progress;

            return Ok(new
            {
                message = "Data ingestion started",
                ingestionId = progress.Id,
                status = progress.Status.ToString(),
                estimatedDuration = EstimateDuration(config)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting data ingestion");
            return StatusCode(500, new { error = "Failed to start data ingestion" });
        }
    }

    [HttpGet("progress/{ingestionId}")]
    public IActionResult GetProgress(string ingestionId)
    {
        try
        {
            if (!_activeIngestions.TryGetValue(ingestionId, out var progress))
            {
                return NotFound(new { error = "Ingestion not found" });
            }

            var progressPercentage = progress.TotalRepositories > 0
                ? (double)progress.ProcessedRepositories / progress.TotalRepositories * 100
                : 0;

            return Ok(new
            {
                progress.Id,
                status = progress.Status.ToString(),
                startTime = progress.StartTime,
                endTime = progress.EndTime,
                duration = progress.Duration,
                progressPercentage = Math.Round(progressPercentage, 2),
                totalRepositories = progress.TotalRepositories,
                processedRepositories = progress.ProcessedRepositories,
                failedRepositories = progress.FailedRepositories,
                processedPRs = progress.ProcessedPRs,
                totalIssues = progress.TotalIssues,
                totalComments = progress.TotalComments,
                errorMessage = progress.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ingestion progress");
            return StatusCode(500, new { error = "Failed to get progress" });
        }
    }

    [HttpGet("status")]
    public IActionResult GetAllIngestions()
    {
        try
        {
            var ingestions = _activeIngestions.Values.Select(p => new
            {
                p.Id,
                status = p.Status.ToString(),
                p.StartTime,
                p.EndTime,
                p.Duration,
                p.ProcessedRepositories,
                p.TotalRepositories,
                p.ProcessedPRs,
                p.TotalIssues
            }).ToList();

            return Ok(new
            {
                totalIngestions = ingestions.Count,
                activeIngestions = ingestions.Count(i => i.status == "Running"),
                completedIngestions = ingestions.Count(i => i.status == "Completed"),
                failedIngestions = ingestions.Count(i => i.status == "Failed"),
                ingestions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all ingestions");
            return StatusCode(500, new { error = "Failed to get ingestions" });
        }
    }

    [HttpPost("stop/{ingestionId}")]
    public IActionResult StopIngestion(string ingestionId)
    {
        try
        {
            if (!_activeIngestions.TryGetValue(ingestionId, out var progress))
            {
                return NotFound(new { error = "Ingestion not found" });
            }

            if (progress.Status == IngestionStatus.Running)
            {
                progress.Status = IngestionStatus.Paused;
                _logger.LogInformation("Ingestion {IngestionId} paused", ingestionId);
            }

            return Ok(new { message = "Ingestion stopped", ingestionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping ingestion");
            return StatusCode(500, new { error = "Failed to stop ingestion" });
        }
    }

    [HttpDelete("clear/{ingestionId}")]
    public IActionResult ClearIngestion(string ingestionId)
    {
        try
        {
            if (_activeIngestions.Remove(ingestionId))
            {
                _logger.LogInformation("Ingestion {IngestionId} cleared", ingestionId);
                return Ok(new { message = "Ingestion cleared", ingestionId });
            }

            return NotFound(new { error = "Ingestion not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing ingestion");
            return StatusCode(500, new { error = "Failed to clear ingestion" });
        }
    }

    [HttpPost("resume/{ingestionId}")]
    public async Task<IActionResult> ResumeIngestion(string ingestionId)
    {
        try
        {
            if (!_activeIngestions.TryGetValue(ingestionId, out var progress))
            {
                return NotFound(new { error = "Ingestion not found" });
            }

            if (progress.Status != IngestionStatus.Paused)
            {
                return BadRequest(new { error = "Ingestion is not paused" });
            }

            // Resume ingestion logic would go here
            // For now, just change status
            progress.Status = IngestionStatus.Running;

            return Ok(new { message = "Ingestion resumed", ingestionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming ingestion");
            return StatusCode(500, new { error = "Failed to resume ingestion" });
        }
    }

    [HttpGet("recommendations")]
    public IActionResult GetIngestionRecommendations()
    {
        try
        {
            var recommendations = new
            {
                popularRepositories = new[]
                {
                    "microsoft/dotnet",
                    "dotnet/core",
                    "dotnet/aspnetcore",
                    "dotnet/efcore",
                    "dotnet/runtime",
                    "microsoft/vscode",
                    "PowerShell/PowerShell",
                    "microsoft/TypeScript",
                    "microsoft/monaco-editor",
                    "microsoft/ApplicationInsights-dotnet"
                },
                suggestedConfig = new
                {
                    maxRepositories = 50,
                    maxPRsPerRepository = 500,
                    startDate = DateTime.UtcNow.AddMonths(-6).ToString("yyyy-MM-dd"),
                    endDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    languages = new[] { "csharp" }
                },
                estimatedDuration = "2-4 hours for 50 repositories",
                memoryRequirements = "2-4 GB RAM recommended",
                rateLimits = new
                {
                    githubApi = "5000 requests/hour (authenticated)",
                    recommendedDelay = "100ms between requests"
                }
            };

            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations");
            return StatusCode(500, new { error = "Failed to get recommendations" });
        }
    }

    private string EstimateDuration(IngestionConfig config)
    {
        var estimatedRepos = Math.Min(config.MaxRepositories, 100);
        var estimatedPRsPerRepo = Math.Min(config.MaxPRsPerRepository, 1000);
        var totalPRs = estimatedRepos * estimatedPRsPerRepo;

        // Rough estimate: 2 seconds per PR (including rate limiting)
        var estimatedSeconds = totalPRs * 2;
        var hours = estimatedSeconds / 3600;
        var minutes = (estimatedSeconds % 3600) / 60;

        return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
    }
}
