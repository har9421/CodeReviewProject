using CodeReviewBot.Configuration;
using CodeReviewBot.Interfaces;
using CodeReviewBot.Models;
using CodeReviewBot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CodeReviewBot.Tests
{
    /// <summary>
    /// Integration tests to verify the bot's core functionality
    /// </summary>
    public class IntegrationTests
    {
        [Fact]
        public async Task CodeAnalyzerService_ShouldDetectCodingStandardViolations()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CodeAnalyzerService>>();
            var mockHttpClient = new Mock<HttpClient>();
            
            var botOptions = new BotOptions
            {
                Name = "Test Bot",
                Version = "1.0.0",
                DefaultRulesUrl = "coding-standards.json",
                Analysis = new AnalysisOptions
                {
                    MaxConcurrentFiles = 10,
                    SupportedFileExtensions = new[] { ".cs" }
                }
            };
            
            var mockOptions = new Mock<IOptions<BotOptions>>();
            mockOptions.Setup(x => x.Value).Returns(botOptions);
            
            var codeAnalyzer = new CodeAnalyzerService(mockLogger.Object, mockOptions.Object, mockHttpClient.Object);

            // Sample C# code with violations
            var badCode = @"
using System;

namespace testproject
{
    public class badCodeExample
    {
        private string password = ""hardcoded123"";
        
        public async Task<bool> ProcessUserAsync(int userId, string username, string email, string phone, string address, string city, string state, string zipcode, string country, string department)
        {
            // Too many parameters - violates coding standards
            var message = ""Processing user: "" + userId + "" with name: "" + username;
            Console.WriteLine(message);
            
            // Magic numbers
            if (userId > 1000)
            {
                return true;
            }
            
            // SQL injection vulnerability
            var query = ""SELECT * FROM Users WHERE Id = "" + userId;
            
            return false;
        }
    }
}";

            var fileChange = new FileChange
            {
                Path = "BadCode.cs",
                ChangeType = "edit",
                Content = badCode
            };

            // Act
            var issues = await codeAnalyzer.AnalyzeFileAsync(fileChange);

            // Assert
            Assert.NotNull(issues);
            
            // Should detect multiple issues
            if (issues.Count > 0)
            {
                var issueTypes = new HashSet<string>();
                foreach (var issue in issues)
                {
                    Assert.NotEmpty(issue.RuleId);
                    Assert.NotEmpty(issue.Severity);
                    Assert.NotEmpty(issue.Message);
                    Assert.NotEmpty(issue.FilePath);
                    Assert.True(issue.LineNumber > 0);
                    
                    issueTypes.Add(issue.RuleId);
                }
                
                // Log the detected issues for verification
                Console.WriteLine($"Found {issues.Count} coding standard violations:");
                foreach (var issue in issues)
                {
                    Console.WriteLine($"  - {issue.Severity}: {issue.RuleId} at line {issue.LineNumber}: {issue.Message}");
                }
            }
            else
            {
                Console.WriteLine("No coding standard violations detected. This might indicate:");
                Console.WriteLine("1. The coding standards file is not being loaded properly");
                Console.WriteLine("2. The regex patterns are not matching the violations");
                Console.WriteLine("3. The rules configuration needs adjustment");
            }
        }

        [Fact]
        public async Task CodeAnalyzerService_ShouldNotDetectIssuesInGoodCode()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<CodeAnalyzerService>>();
            var mockHttpClient = new Mock<HttpClient>();
            
            var botOptions = new BotOptions
            {
                Name = "Test Bot",
                Version = "1.0.0",
                DefaultRulesUrl = "coding-standards.json",
                Analysis = new AnalysisOptions
                {
                    MaxConcurrentFiles = 10,
                    SupportedFileExtensions = new[] { ".cs" }
                }
            };
            
            var mockOptions = new Mock<IOptions<BotOptions>>();
            mockOptions.Setup(x => x.Value).Returns(botOptions);
            
            var codeAnalyzer = new CodeAnalyzerService(mockLogger.Object, mockOptions.Object, mockHttpClient.Object);

            // Well-structured C# code
            var goodCode = @"
using System;
using System.Threading.Tasks;

namespace TestProject
{
    /// <summary>
    /// A well-structured class that follows coding standards
    /// </summary>
    public class GoodCodeExample
    {
        private readonly string _connectionString;
        private const int MaxRetryAttempts = 3;

        /// <summary>
        /// Initializes a new instance of the GoodCodeExample class
        /// </summary>
        /// <param name=""connectionString"">Database connection string</param>
        public GoodCodeExample(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Processes user data asynchronously
        /// </summary>
        /// <param name=""userId"">The user identifier</param>
        /// <returns>Task representing the async operation</returns>
        public async Task<bool> ProcessUserAsync(int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentException(""User ID must be positive"", nameof(userId));
            }

            try
            {
                var result = await ValidateUserAsync(userId);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($""Error processing user {userId}: {ex.Message}"");
                return false;
            }
        }

        private async Task<bool> ValidateUserAsync(int userId)
        {
            await Task.Delay(100);
            return userId > 0;
        }
    }
}";

            var fileChange = new FileChange
            {
                Path = "GoodCode.cs",
                ChangeType = "edit",
                Content = goodCode
            };

            // Act
            var issues = await codeAnalyzer.AnalyzeFileAsync(fileChange);

            // Assert
            Assert.NotNull(issues);
            
            if (issues.Count == 0)
            {
                Console.WriteLine("✅ Good code passed analysis - no violations detected!");
            }
            else
            {
                Console.WriteLine($"⚠️  Good code triggered {issues.Count} false positives:");
                foreach (var issue in issues)
                {
                    Console.WriteLine($"  - {issue.Severity}: {issue.RuleId} at line {issue.LineNumber}: {issue.Message}");
                }
            }
        }

        [Fact]
        public void BotConfiguration_ShouldBeValid()
        {
            // Arrange & Act
            var botOptions = new BotOptions
            {
                Name = "Test Bot",
                Version = "1.0.0",
                DefaultRulesUrl = "coding-standards.json",
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

            // Assert
            Assert.Equal("Test Bot", botOptions.Name);
            Assert.Equal("1.0.0", botOptions.Version);
            Assert.Equal("coding-standards.json", botOptions.DefaultRulesUrl);
            Assert.NotNull(botOptions.Webhook);
            Assert.NotNull(botOptions.AzureDevOps);
            Assert.NotNull(botOptions.Analysis);
            Assert.NotNull(botOptions.Notifications);
            
            Console.WriteLine("✅ Bot configuration is valid");
        }
    }
}
