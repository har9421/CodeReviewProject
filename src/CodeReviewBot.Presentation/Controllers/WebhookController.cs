using CodeReviewBot.Application.DTOs;
using CodeReviewBot.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace CodeReviewBot.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IPullRequestAnalysisService _pullRequestAnalysisService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IPullRequestAnalysisService pullRequestAnalysisService,
        ILogger<WebhookController> logger)
    {
        _pullRequestAnalysisService = pullRequestAnalysisService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook([FromBody] JObject payload)
    {
        try
        {
            var eventType = payload["eventType"]?.ToString();
            _logger.LogInformation("Received webhook: {EventType}", eventType);

            if (string.IsNullOrEmpty(eventType))
            {
                return BadRequest("Missing eventType in payload");
            }

            // Only process pull request events
            if (eventType != "git.pullrequest.created" && eventType != "git.pullrequest.updated")
            {
                _logger.LogInformation("Ignoring event type: {EventType}", eventType);
                return Ok(new { message = "Event type ignored" });
            }

            var pullRequestIdStr = payload["resource"]?["pullRequestId"]?.ToString();
            var projectName = payload["resource"]?["repository"]?["project"]?["name"]?.ToString();
            var repositoryName = payload["resource"]?["repository"]?["name"]?.ToString();
            var organizationUrl = payload["resourceContainers"]?["project"]?["baseUrl"]?.ToString();

            if (!int.TryParse(pullRequestIdStr, out var pullRequestId))
            {
                _logger.LogError("Invalid pull request ID: {PullRequestId}", pullRequestIdStr);
                return BadRequest("Invalid pull request ID");
            }

            if (string.IsNullOrEmpty(organizationUrl) || string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(repositoryName))
            {
                _logger.LogError("Missing required PR information: Organization={OrganizationUrl}, Project={ProjectName}, Repository={RepositoryName}",
                    organizationUrl, projectName, repositoryName);
                return BadRequest("Missing required pull request information");
            }

            // Get personal access token from environment or configuration
            var personalAccessToken = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT") ?? "";

            if (string.IsNullOrEmpty(personalAccessToken))
            {
                _logger.LogWarning("AZURE_DEVOPS_PAT environment variable not set. Code analysis will be limited.");
                return Ok(new { message = "PAT not configured, analysis skipped" });
            }

            var request = new AnalyzePullRequestRequest
            {
                EventType = eventType,
                OrganizationUrl = organizationUrl,
                ProjectName = projectName,
                RepositoryName = repositoryName,
                PullRequestId = pullRequestId,
                PersonalAccessToken = personalAccessToken
            };

            var result = await _pullRequestAnalysisService.AnalyzePullRequestAsync(request);

            if (result.Success)
            {
                _logger.LogInformation("Pull request analysis completed successfully. Found {IssueCount} issues, posted {CommentCount} comments.",
                    result.IssuesFound, result.CommentsPosted);

                return Ok(new
                {
                    message = "Analysis completed successfully",
                    issuesFound = result.IssuesFound,
                    commentsPosted = result.CommentsPosted
                });
            }
            else
            {
                _logger.LogError("Pull request analysis failed: {Error}", result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            message = "Code Review Bot is running"
        });
    }
}
