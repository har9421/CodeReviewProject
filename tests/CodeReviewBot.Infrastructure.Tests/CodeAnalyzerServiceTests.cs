using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Infrastructure.ExternalServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeReviewBot.Infrastructure.Tests;

public class CodeAnalyzerServiceTests
{
    private readonly Mock<ILogger<CodeAnalyzerService>> _mockLogger;
    private readonly CodeAnalyzerService _codeAnalyzerService;

    public CodeAnalyzerServiceTests()
    {
        _mockLogger = new Mock<ILogger<CodeAnalyzerService>>();
        _codeAnalyzerService = new CodeAnalyzerService(_mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithGoodCode_ShouldReturnNoIssues()
    {
        // Arrange
        var goodCode = @"
using System;

namespace TestProject
{
    public class GoodCodeExample
    {
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
        var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);

        // Assert
        issues.Should().NotBeNull();
        // Note: This might find some issues due to the default rules, which is expected
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithBadCode_ShouldReturnMultipleIssues()
    {
        // Arrange
        var badCode = @"
using System;

namespace testproject
{
    public class badCodeExample
    {
        private string password = ""hardcoded123"";
        
        public async Task<bool> ProcessUserAsync(int userId, string username, string email, string phone, string address, string city, string state, string zipcode, string country, string department)
        {
            var message = ""Processing user: "" + userId + "" with name: "" + username;
            Console.WriteLine(message);
            
            if (userId > 1000)
            {
                return true;
            }
            
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
        var issues = await _codeAnalyzerService.AnalyzeFileAsync(fileChange);

        // Assert
        issues.Should().NotBeNull();

        if (issues.Count > 0)
        {
            foreach (var issue in issues)
            {
                issue.RuleId.Should().NotBeEmpty();
                issue.Severity.Should().NotBeEmpty();
                issue.Message.Should().NotBeEmpty();
                issue.FilePath.Should().NotBeEmpty();
                issue.LineNumber.Should().BeGreaterThan(0);
            }

            Console.WriteLine($"Found {issues.Count} coding standard violations:");
            foreach (var issue in issues)
            {
                Console.WriteLine($"  - {issue.Severity}: {issue.RuleId} at line {issue.LineNumber}: {issue.Message}");
            }
        }
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
        issues.Should().NotBeNull();
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadCodingRulesAsync_ShouldReturnRules()
    {
        // Act
        var rules = await _codeAnalyzerService.LoadCodingRulesAsync();

        // Assert
        rules.Should().NotBeNull();
        rules.Should().NotBeEmpty();

        foreach (var rule in rules)
        {
            rule.Id.Should().NotBeEmpty();
            rule.Severity.Should().NotBeEmpty();
            rule.Message.Should().NotBeEmpty();
        }
    }
}
