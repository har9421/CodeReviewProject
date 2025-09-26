using CodeReviewBot.Configuration;
using CodeReviewBot.Interfaces;
using CodeReviewBot.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CodeReviewBot.Services;

public class WebhookService : IWebhookService
{
    private readonly ILogger<WebhookService> _logger;
    private readonly BotOptions _botOptions;
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ICodeAnalyzerService _codeAnalyzerService;

    public WebhookService(
        ILogger<WebhookService> logger,
        IOptions<BotOptions> botOptions,
        IAzureDevOpsService azureDevOpsService,
        ICodeAnalyzerService codeAnalyzerService)
    {
        _logger = logger;
        _botOptions = botOptions.Value;
        _azureDevOpsService = azureDevOpsService;
        _codeAnalyzerService = codeAnalyzerService;
    }

    public async Task ProcessWebhookAsync(string eventType, JObject payload, string signature)
    {
        var jsonPayload = payload.ToString(Formatting.None);

        // Only validate signature if it's provided and secret is configured
        if (!string.IsNullOrEmpty(signature) && !string.IsNullOrEmpty(_botOptions.Webhook.Secret))
        {
            if (!ValidateSignature(jsonPayload, signature, _botOptions.Webhook.Secret))
            {
                _logger.LogWarning("Webhook signature validation failed for event type: {EventType}", eventType);
                throw new UnauthorizedAccessException("Invalid webhook signature.");
            }
            _logger.LogInformation("Webhook signature validated successfully for event type: {EventType}", eventType);
        }
        else
        {
            _logger.LogInformation("Processing webhook without signature validation for event type: {EventType}", eventType);
        }

        switch (eventType)
        {
            case "git.pullrequest.created":
            case "git.pullrequest.updated":
                await HandlePullRequestEvent(payload);
                break;
            default:
                _logger.LogInformation("Unhandled event type: {EventType}", eventType);
                break;
        }
    }

    public bool ValidateSignature(string jsonPayload, string signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        using (var hmac = new HMACSHA256(keyBytes))
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(jsonPayload));
            var expectedSignature = Convert.ToBase64String(hash);
            return string.Equals(expectedSignature, signatureHeader, StringComparison.Ordinal);
        }
    }

    private async Task HandlePullRequestEvent(JObject payload)
    {
        try
        {
            var pullRequestIdStr = payload["resource"]?["pullRequestId"]?.ToString();
            var projectName = payload["resource"]?["repository"]?["project"]?["name"]?.ToString();
            var repositoryName = payload["resource"]?["repository"]?["name"]?.ToString();
            var organizationUrl = payload["resourceContainers"]?["project"]?["baseUrl"]?.ToString();

            if (!int.TryParse(pullRequestIdStr, out var pullRequestId))
            {
                _logger.LogError("Invalid pull request ID: {PullRequestId}", pullRequestIdStr);
                return;
            }

            if (string.IsNullOrEmpty(organizationUrl) || string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(repositoryName))
            {
                _logger.LogError("Missing required PR information: Organization={OrganizationUrl}, Project={ProjectName}, Repository={RepositoryName}", 
                    organizationUrl, projectName, repositoryName);
                return;
            }

            _logger.LogInformation("Processing PR {PullRequestId} in {Project}/{Repository}",
                pullRequestId, projectName, repositoryName);

            // Get personal access token from configuration
            var personalAccessToken = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT") ?? 
                                     _botOptions.AzureDevOps?.PersonalAccessToken ?? "";
            
            if (string.IsNullOrEmpty(personalAccessToken))
            {
                _logger.LogWarning("AZURE_DEVOPS_PAT environment variable not set. Code analysis will be limited.");
                return;
            }
            
            _logger.LogInformation("Using PAT for Azure DevOps API calls (length: {PatLength})", personalAccessToken.Length);

            // 1. Fetch PR details
            var prDetails = await _azureDevOpsService.GetPullRequestDetailsAsync(
                organizationUrl, projectName, repositoryName, pullRequestId, personalAccessToken);

            if (prDetails == null)
            {
                _logger.LogError("Failed to fetch PR details for PR {PullRequestId}", pullRequestId);
                return;
            }

            // 2. Fetch PR changes
            var fileChanges = await _azureDevOpsService.GetPullRequestChangesAsync(
                organizationUrl, projectName, repositoryName, pullRequestId, personalAccessToken);

            if (!fileChanges.Any())
            {
                _logger.LogInformation("No C# files changed in PR {PullRequestId}", pullRequestId);
                return;
            }

            // 3. Analyze each changed file
            var allIssues = new List<CodeIssue>();
            foreach (var fileChange in fileChanges)
            {
                var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);
                allIssues.AddRange(issues);
            }

            _logger.LogInformation("Found {IssueCount} issues across {FileCount} files in PR {PullRequestId}", 
                allIssues.Count, fileChanges.Count, pullRequestId);

            // 4. Post comments for issues (limit to avoid spam)
            var commentCount = 0;
            var maxComments = _botOptions.Notifications.MaxCommentsPerFile;

            foreach (var issue in allIssues.Take(maxComments))
            {
                if (commentCount >= maxComments)
                {
                    _logger.LogInformation("Reached maximum comment limit ({MaxComments}) for PR {PullRequestId}", 
                        maxComments, pullRequestId);
                    break;
                }

                var comment = new PullRequestComment
                {
                    Content = $":robot: **{_botOptions.Name}**\n\n**{issue.Severity}**: {issue.Message}\n\n" +
                              (!string.IsNullOrEmpty(issue.Suggestion) ? $"💡 **Suggestion**: {issue.Suggestion}\n\n" : "") +
                              $"📋 **Rule**: {issue.RuleId}",
                    FilePath = issue.FilePath,
                    LineNumber = issue.LineNumber,
                    Severity = issue.Severity
                };

                var success = await _azureDevOpsService.PostCommentAsync(
                    organizationUrl, projectName, repositoryName, pullRequestId, personalAccessToken, comment);

                if (success)
                {
                    commentCount++;
                    _logger.LogInformation("Posted comment for issue {RuleId} in file {FilePath}:{LineNumber}", 
                        issue.RuleId, issue.FilePath, issue.LineNumber);
                }
                else
                {
                    _logger.LogWarning("Failed to post comment for issue {RuleId}", issue.RuleId);
                }

                // Add small delay to avoid rate limiting
                await Task.Delay(500);
            }

            // 5. Post summary comment if there were issues
            if (allIssues.Any())
            {
                var summaryComment = new PullRequestComment
                {
                    Content = $":robot: **{_botOptions.Name} Analysis Summary**\n\n" +
                              $"Found **{allIssues.Count}** code quality issues:\n\n" +
                              $"• **Errors**: {allIssues.Count(i => i.Severity == "Error")}\n" +
                              $"• **Warnings**: {allIssues.Count(i => i.Severity == "Warning")}\n" +
                              $"• **Info**: {allIssues.Count(i => i.Severity == "Info")}\n\n" +
                              $"Files analyzed: {fileChanges.Count}\n" +
                              $"Comments posted: {commentCount}",
                    FilePath = "",
                    LineNumber = 0,
                    Severity = "Info"
                };

                await _azureDevOpsService.PostCommentAsync(
                    organizationUrl, projectName, repositoryName, pullRequestId, personalAccessToken, summaryComment);

                _logger.LogInformation("Posted summary comment for PR {PullRequestId}", pullRequestId);
            }

            _logger.LogInformation("PR {PullRequestId} analysis completed successfully. Found {IssueCount} issues, posted {CommentCount} comments.", 
                pullRequestId, allIssues.Count, commentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pull request event");
        }
    }
}