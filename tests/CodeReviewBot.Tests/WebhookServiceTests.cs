using CodeReviewBot.Configuration;
using CodeReviewBot.Interfaces;
using CodeReviewBot.Models;
using CodeReviewBot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CodeReviewBot.Tests
{
    public class WebhookServiceTests
    {
        private readonly Mock<ILogger<WebhookService>> _mockLogger;
        private readonly Mock<IAzureDevOpsService> _mockAzureDevOpsService;
        private readonly Mock<ICodeAnalyzerService> _mockCodeAnalyzerService;
        private readonly Mock<IOptions<BotOptions>> _mockOptions;
        private readonly BotOptions _botOptions;
        private readonly WebhookService _webhookService;

        public WebhookServiceTests()
        {
            _mockLogger = new Mock<ILogger<WebhookService>>();
            _mockAzureDevOpsService = new Mock<IAzureDevOpsService>();
            _mockCodeAnalyzerService = new Mock<ICodeAnalyzerService>();
            
            _botOptions = new BotOptions
            {
                Name = "Test Bot",
                Version = "1.0.0",
                Webhook = new WebhookOptions
                {
                    Secret = "test-secret",
                    AllowedEvents = new List<string> { "git.pullrequest.created", "git.pullrequest.updated" },
                    TimeoutSeconds = 30
                },
                AzureDevOps = new AzureDevOpsOptions
                {
                    BaseUrl = "https://dev.azure.com",
                    ApiVersion = "7.0",
                    PersonalAccessToken = "test-pat"
                },
                Analysis = new AnalysisOptions
                {
                    MaxConcurrentFiles = 10,
                    SupportedFileExtensions = new[] { ".cs" }
                },
                Notifications = new NotificationsOptions
                {
                    EnableComments = true,
                    MaxCommentsPerFile = 50
                }
            };

            _mockOptions = new Mock<IOptions<BotOptions>>();
            _mockOptions.Setup(x => x.Value).Returns(_botOptions);

            _webhookService = new WebhookService(
                _mockLogger.Object,
                _mockOptions.Object,
                _mockAzureDevOpsService.Object,
                _mockCodeAnalyzerService.Object);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithValidPullRequestCreatedEvent_ShouldProcessSuccessfully()
        {
            // Arrange
            var webhookPayload = CreatePullRequestWebhookPayload("git.pullrequest.created");
            
            var prDetails = new PullRequestDetails
            {
                PullRequestId = 123,
                Title = "Test PR",
                Description = "Test description"
            };

            var fileChanges = new List<FileChange>
            {
                new FileChange
                {
                    Path = "TestFile.cs",
                    ChangeType = "edit",
                    Content = "public class Test { }"
                }
            };

            var codeIssues = new List<CodeIssue>
            {
                new CodeIssue
                {
                    RuleId = "TEST_RULE",
                    Severity = "Warning",
                    Message = "Test issue",
                    FilePath = "TestFile.cs",
                    LineNumber = 1,
                    // ColumnNumber property removed from CodeIssue model
                }
            };

            _mockAzureDevOpsService
                .Setup(x => x.GetPullRequestDetailsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                    It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(prDetails);

            _mockAzureDevOpsService
                .Setup(x => x.GetPullRequestChangesAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                    It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(fileChanges);

            _mockCodeAnalyzerService
                .Setup(x => x.AnalyzeFileAsync(It.IsAny<FileChange>()))
                .ReturnsAsync(codeIssues);

            _mockAzureDevOpsService
                .Setup(x => x.PostCommentAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PullRequestComment>()))
                .ReturnsAsync(true);

            // Act
            await _webhookService.ProcessWebhookAsync("git.pullrequest.created", webhookPayload, "");

            // Assert
            _mockAzureDevOpsService.Verify(x => x.GetPullRequestDetailsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<int>(), It.IsAny<string>()), Times.Once);

            _mockAzureDevOpsService.Verify(x => x.GetPullRequestChangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<int>(), It.IsAny<string>()), Times.Once);

            _mockCodeAnalyzerService.Verify(x => x.AnalyzeFileAsync(It.IsAny<FileChange>()), Times.Once);

            _mockAzureDevOpsService.Verify(x => x.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PullRequestComment>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithNoFileChanges_ShouldNotAnalyzeCode()
        {
            // Arrange
            var webhookPayload = CreatePullRequestWebhookPayload("git.pullrequest.created");
            
            var prDetails = new PullRequestDetails
            {
                PullRequestId = 123,
                Title = "Test PR"
            };

            var emptyFileChanges = new List<FileChange>();

            _mockAzureDevOpsService
                .Setup(x => x.GetPullRequestDetailsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                    It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(prDetails);

            _mockAzureDevOpsService
                .Setup(x => x.GetPullRequestChangesAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                    It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(emptyFileChanges);

            // Act
            await _webhookService.ProcessWebhookAsync("git.pullrequest.created", webhookPayload, "");

            // Assert
            _mockCodeAnalyzerService.Verify(x => x.AnalyzeFileAsync(It.IsAny<FileChange>()), Times.Never);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithInvalidPullRequestId_ShouldNotProcess()
        {
            // Arrange
            var webhookPayload = CreatePullRequestWebhookPayloadWithInvalidId();

            // Act
            await _webhookService.ProcessWebhookAsync("git.pullrequest.created", webhookPayload, "");

            // Assert
            _mockAzureDevOpsService.Verify(x => x.GetPullRequestDetailsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithUnhandledEventType_ShouldNotProcess()
        {
            // Arrange
            var webhookPayload = new JObject();

            // Act
            await _webhookService.ProcessWebhookAsync("git.push", webhookPayload, "");

            // Assert
            _mockAzureDevOpsService.Verify(x => x.GetPullRequestDetailsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        private JObject CreatePullRequestWebhookPayload(string eventType)
        {
            return new JObject
            {
                ["eventType"] = eventType,
                ["resource"] = new JObject
                {
                    ["pullRequestId"] = 123,
                    ["repository"] = new JObject
                    {
                        ["name"] = "test-repo",
                        ["project"] = new JObject
                        {
                            ["name"] = "test-project"
                        }
                    }
                },
                ["resourceContainers"] = new JObject
                {
                    ["project"] = new JObject
                    {
                        ["baseUrl"] = "https://dev.azure.com/testorg"
                    }
                }
            };
        }

        private JObject CreatePullRequestWebhookPayloadWithInvalidId()
        {
            return new JObject
            {
                ["eventType"] = "git.pullrequest.created",
                ["resource"] = new JObject
                {
                    ["pullRequestId"] = "invalid-id",
                    ["repository"] = new JObject
                    {
                        ["name"] = "test-repo",
                        ["project"] = new JObject
                        {
                            ["name"] = "test-project"
                        }
                    }
                },
                ["resourceContainers"] = new JObject
                {
                    ["project"] = new JObject
                    {
                        ["baseUrl"] = "https://dev.azure.com/testorg"
                    }
                }
            };
        }
    }
}
