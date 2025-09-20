using CodeReviewBot.Interfaces;
using CodeReviewBot.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Text;

namespace CodeReviewBot.Controllers;

[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(IWebhookService webhookService, ILogger<WebhookController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        _logger.LogInformation("Received webhook request.");

        // Read the raw request body
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var jsonContent = await reader.ReadToEndAsync();

        // Get headers for validation
        var signature = Request.Headers["X-Vss-Signature"].FirstOrDefault();
        var eventType = Request.Headers["X-Vss-Event"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(eventType))
        {
            _logger.LogWarning("Missing X-Vss-Signature or X-Vss-Event header.");
            return Unauthorized("Missing signature or event type header.");
        }

        try
        {
            var webhookEvent = JObject.Parse(jsonContent);
            await _webhookService.ProcessWebhookAsync(eventType, webhookEvent, signature);
            return Ok();
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON payload received.");
            return BadRequest("Invalid JSON payload.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Webhook signature validation failed.");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook.");
            return StatusCode(500, "Internal server error.");
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