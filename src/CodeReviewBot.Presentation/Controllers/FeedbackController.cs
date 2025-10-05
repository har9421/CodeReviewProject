using CodeReviewBot.Application.Interfaces;
using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CodeReviewBot.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly ILearningService _learningService;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(ILearningService learningService, ILogger<FeedbackController> logger)
    {
        _learningService = learningService;
        _logger = logger;
    }

    [HttpPost("issue-feedback")]
    public async Task<IActionResult> SubmitIssueFeedback([FromBody] IssueFeedbackRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var feedback = new DeveloperFeedback
            {
                IssueId = request.IssueId,
                RuleId = request.RuleId,
                FilePath = request.FilePath,
                LineNumber = request.LineNumber,
                Type = request.FeedbackType,
                Comment = request.Comment,
                Timestamp = DateTime.UtcNow
            };

            await _learningService.UpdateRuleEffectivenessAsync(request.RuleId, request.FeedbackType);

            _logger.LogInformation("Received feedback for issue {IssueId}: {FeedbackType}",
                request.IssueId, request.FeedbackType);

            return Ok(new { message = "Feedback recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing issue feedback");
            return StatusCode(500, new { error = "Failed to process feedback" });
        }
    }

    [HttpGet("insights")]
    public async Task<IActionResult> GetLearningInsights()
    {
        try
        {
            var insights = await _learningService.GetLearningInsightsAsync();
            return Ok(insights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving learning insights");
            return StatusCode(500, new { error = "Failed to retrieve insights" });
        }
    }

    [HttpGet("rule-effectiveness")]
    public async Task<IActionResult> GetRuleEffectiveness()
    {
        try
        {
            var effectiveness = await _learningService.GetRuleEffectivenessAsync();
            return Ok(effectiveness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rule effectiveness");
            return StatusCode(500, new { error = "Failed to retrieve rule effectiveness" });
        }
    }

    [HttpPost("optimize-rules")]
    public async Task<IActionResult> OptimizeRules()
    {
        try
        {
            await _learningService.OptimizeRulesAsync();
            _logger.LogInformation("Rule optimization completed");
            return Ok(new { message = "Rules optimized successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing rules");
            return StatusCode(500, new { error = "Failed to optimize rules" });
        }
    }

    [HttpPost("bulk-feedback")]
    public async Task<IActionResult> SubmitBulkFeedback([FromBody] BulkFeedbackRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var processedCount = 0;
            foreach (var feedbackItem in request.FeedbackItems)
            {
                await _learningService.UpdateRuleEffectivenessAsync(
                    feedbackItem.RuleId,
                    feedbackItem.FeedbackType);
                processedCount++;
            }

            _logger.LogInformation("Processed {Count} bulk feedback items", processedCount);
            return Ok(new { message = $"Processed {processedCount} feedback items successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bulk feedback");
            return StatusCode(500, new { error = "Failed to process bulk feedback" });
        }
    }
}

public class IssueFeedbackRequest
{
    public string IssueId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public FeedbackType FeedbackType { get; set; }
    public string? Comment { get; set; }
}

public class BulkFeedbackRequest
{
    public List<FeedbackItem> FeedbackItems { get; set; } = new();
}

public class FeedbackItem
{
    public string RuleId { get; set; } = string.Empty;
    public FeedbackType FeedbackType { get; set; }
}
