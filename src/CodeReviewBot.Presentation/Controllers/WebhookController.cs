using CodeReviewBot.Application.DTOs;
using CodeReviewBot.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;

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
    public async Task<IActionResult> HandleWebhook()
    {
        try
        {
            // Read the request body manually to avoid model binding issues
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                _logger.LogWarning("Received empty webhook payload");
                return BadRequest("Empty payload");
            }

            var payload = JObject.Parse(requestBody);
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

            // Extract organization URL from the repository URL
            var repositoryUrl = payload["resource"]?["repository"]?["url"]?.ToString();
            var organizationUrl = "";

            if (!string.IsNullOrEmpty(repositoryUrl))
            {
                // Extract organization URL from repository URL
                // Example: https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/...
                var uri = new Uri(repositoryUrl);
                organizationUrl = $"{uri.Scheme}://{uri.Host}";
            }

            if (!int.TryParse(pullRequestIdStr, out var pullRequestId))
            {
                _logger.LogError("Invalid pull request ID: {PullRequestId}", pullRequestIdStr);
                return BadRequest("Invalid pull request ID");
            }

            if (string.IsNullOrEmpty(organizationUrl) || string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(repositoryName))
            {
                _logger.LogError("Missing required PR information: Organization={OrganizationUrl}, Project={ProjectName}, Repository={RepositoryName}",
                    organizationUrl, projectName, repositoryName);

                // Log the full payload for debugging
                _logger.LogError("Full webhook payload: {Payload}", payload.ToString());

                return BadRequest($"Missing required pull request information. Organization: {organizationUrl}, Project: {projectName}, Repository: {repositoryName}");
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

    [HttpPost("test")]
    public IActionResult TestWebhook([FromBody] JObject payload)
    {
        try
        {
            _logger.LogInformation("Test webhook received payload: {Payload}", payload.ToString());

            var eventType = payload["eventType"]?.ToString();
            var pullRequestIdStr = payload["resource"]?["pullRequestId"]?.ToString();
            var projectName = payload["resource"]?["repository"]?["project"]?["name"]?.ToString();
            var repositoryName = payload["resource"]?["repository"]?["name"]?.ToString();
            var repositoryUrl = payload["resource"]?["repository"]?["url"]?.ToString();

            return Ok(new
            {
                message = "Test webhook received successfully",
                extracted = new
                {
                    eventType,
                    pullRequestId = pullRequestIdStr,
                    projectName,
                    repositoryName,
                    repositoryUrl,
                    organizationUrl = !string.IsNullOrEmpty(repositoryUrl) ? new Uri(repositoryUrl).GetLeftPart(UriPartial.Authority) : "Could not extract"
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test webhook");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
