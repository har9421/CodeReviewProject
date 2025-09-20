using CodeReviewBot.Services;
using CodeReviewBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeReviewBot.Configuration;
using System.Text.Json;

namespace CodeReviewBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhookController> _logger;
    private readonly BotOptions _options;

    public WebhookController(
        IWebhookService webhookService,
        ILogger<WebhookController> logger,
        IOptions<BotOptions> options)
    {
        _webhookService = webhookService;
        _logger = logger;
        _options = options.Value;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook([FromBody] AzureDevOpsWebhook payload)
    {
        try
        {
            _logger.LogInformation("Received webhook: {EventType}", payload.EventType);

            // Validate webhook
            if (!IsValidWebhook(payload))
            {
                _logger.LogWarning("Invalid webhook received: {EventType}", payload.EventType);
                return BadRequest("Invalid webhook");
            }

            // Process the webhook
            var result = await _webhookService.ProcessWebhookAsync(payload);

            if (result.Success)
            {
                _logger.LogInformation("Webhook processed successfully");
                return Ok(new { message = "Webhook processed successfully", analysisId = result.AnalysisId });
            }
            else
            {
                _logger.LogError("Webhook processing failed: {Error}", result.ErrorMessage);
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
            botName = _options.Name,
            version = _options.Version,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("configure")]
    public async Task<IActionResult> Configure([FromBody] BotConfigurationRequest request)
    {
        try
        {
            _logger.LogInformation("Received configuration request from organization: {Organization}", request.Organization);

            var result = await _webhookService.ConfigureBotAsync(request);

            if (result.Success)
            {
                return Ok(new { message = "Bot configured successfully", configurationId = result.ConfigurationId });
            }
            else
            {
                return BadRequest(new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring bot");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private bool IsValidWebhook(AzureDevOpsWebhook payload)
    {
        if (payload == null)
            return false;

        if (string.IsNullOrEmpty(payload.EventType))
            return false;

        // Check if the event type is allowed
        if (!_options.Webhook.AllowedEvents.Contains(payload.EventType))
        {
            _logger.LogWarning("Event type {EventType} is not in allowed list", payload.EventType);
            return false;
        }

        // Validate pull request webhooks
        if (payload.EventType.StartsWith("git.pullrequest."))
        {
            return payload.Resource != null &&
                   !string.IsNullOrEmpty(payload.Resource.Repository?.Id) &&
                   payload.Resource.PullRequestId > 0;
        }

        return true;
    }
}
