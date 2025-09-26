using CodeReviewBot.Configuration;
using CodeReviewBot.Interfaces;
using CodeReviewBot.Models;
using CodeReviewBot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace CodeReviewBot.Tests
{
    public class CodeAnalyzerServiceTests
    {
        private readonly ICodeAnalyzerService _codeAnalyzerService;
        private readonly Mock<ILogger<CodeAnalyzerService>> _mockLogger;
        private readonly Mock<IOptions<BotOptions>> _mockOptions;
        private readonly Mock<HttpClient> _mockHttpClient;
        private readonly BotOptions _botOptions;

        public CodeAnalyzerServiceTests()
        {
            _mockLogger = new Mock<ILogger<CodeAnalyzerService>>();
            _mockHttpClient = new Mock<HttpClient>();
            
            _botOptions = new BotOptions
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
            
            _mockOptions = new Mock<IOptions<BotOptions>>();
            _mockOptions.Setup(x => x.Value).Returns(_botOptions);
            
            _codeAnalyzerService = new CodeAnalyzerService(
                _mockLogger.Object, 
                _mockOptions.Object, 
                _mockHttpClient.Object);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithGoodCode_ShouldReturnNoIssues()
        {
            // Arrange
            var goodCodeContent = File.ReadAllText("../../../../test-files/GoodCode.cs");
            var fileChange = new FileChange
            {
                Path = "GoodCode.cs",
                ChangeType = "edit",
                Content = goodCodeContent
            };

            // Act
            var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);

            // Assert
            Assert.NotNull(issues);
            Assert.Empty(issues);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithBadCode_ShouldReturnMultipleIssues()
        {
            // Arrange
            var badCodeContent = File.ReadAllText("../../../../test-files/BadCode.cs");
            var fileChange = new FileChange
            {
                Path = "BadCode.cs",
                ChangeType = "edit",
                Content = badCodeContent
            };

            // Act
            var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);

            // Assert
            Assert.NotNull(issues);
            Assert.NotEmpty(issues);

            // Verify specific issues are detected
            var issueTypes = issues.Select(i => i.RuleId).ToHashSet();
            
            // Should detect naming convention violations
            Assert.Contains("NAMING_CONVENTIONS_CLASS", issueTypes);
            Assert.Contains("NAMING_CONVENTIONS_NAMESPACE", issueTypes);
            
            // Should detect method parameter count violation
            Assert.Contains("METHOD_PARAMETER_COUNT", issueTypes);
            
            // Should detect string concatenation issue
            Assert.Contains("STRING_CONCATENATION", issueTypes);
            
            // Should detect magic numbers
            Assert.Contains("MAGIC_NUMBERS", issueTypes);
            
            // Should detect nesting depth
            Assert.Contains("NESTING_DEPTH", issueTypes);
            
            // Should detect SQL injection vulnerability
            Assert.Contains("SQL_INJECTION", issueTypes);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithEmptyContent_ShouldReturnEmptyIssues()
        {
            // Arrange
            var fileChange = new FileChange
            {
                Path = "Empty.cs",
                ChangeType = "edit",
                Content = ""
            };

            // Act
            var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);

            // Assert
            Assert.NotNull(issues);
            Assert.Empty(issues);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithNullContent_ShouldReturnEmptyIssues()
        {
            // Arrange
            var fileChange = new FileChange
            {
                Path = "Null.cs",
                ChangeType = "edit",
                Content = null
            };

            // Act
            var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);

            // Assert
            Assert.NotNull(issues);
            Assert.Empty(issues);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithNonCSharpFile_ShouldReturnEmptyIssues()
        {
            // Arrange
            var fileChange = new FileChange
            {
                Path = "readme.txt",
                ChangeType = "edit",
                Content = "This is not C# code"
            };

            // Act
            var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);

            // Assert
            Assert.NotNull(issues);
            Assert.Empty(issues);
        }

        [Fact]
        public async Task AnalyzeFileAsync_IssuesShouldHaveCorrectProperties()
        {
            // Arrange
            var badCodeContent = File.ReadAllText("../../../../test-files/BadCode.cs");
            var fileChange = new FileChange
            {
                Path = "BadCode.cs",
                ChangeType = "edit",
                Content = badCodeContent
            };

            // Act
            var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);

            // Assert
            Assert.NotNull(issues);
            Assert.NotEmpty(issues);

            foreach (var issue in issues)
            {
                Assert.NotEmpty(issue.RuleId);
                Assert.NotEmpty(issue.Severity);
                Assert.NotEmpty(issue.Message);
                Assert.NotEmpty(issue.FilePath);
                Assert.True(issue.LineNumber > 0);
                // ColumnNumber property doesn't exist in CodeIssue model
            }
        }
    }
}

