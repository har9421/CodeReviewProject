using CodeReviewBot.Application.DTOs;
using CodeReviewBot.Application.Interfaces;
using CodeReviewBot.Application.Services;
using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeReviewBot.Application.Tests;

public class PullRequestAnalysisServiceTests
{
    private readonly Mock<IPullRequestRepository> _mockPullRequestRepository;
    private readonly Mock<ICodeAnalyzer> _mockCodeAnalyzer;
    private readonly Mock<ILogger<PullRequestAnalysisService>> _mockLogger;
    private readonly PullRequestAnalysisService _service;

    public PullRequestAnalysisServiceTests()
    {
        _mockPullRequestRepository = new Mock<IPullRequestRepository>();
        _mockCodeAnalyzer = new Mock<ICodeAnalyzer>();
        _mockLogger = new Mock<ILogger<PullRequestAnalysisService>>();

        _service = new PullRequestAnalysisService(
            _mockPullRequestRepository.Object,
            _mockCodeAnalyzer.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzePullRequestAsync_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var request = new AnalyzePullRequestRequest
        {
            EventType = "git.pullrequest.created",
            OrganizationUrl = "https://dev.azure.com/testorg",
            ProjectName = "testproject",
            RepositoryName = "testrepo",
            PullRequestId = 123,
            PersonalAccessToken = "test-pat"
        };

        var pullRequest = new PullRequest
        {
            PullRequestId = 123,
            Title = "Test PR",
            Description = "Test description",
            RepositoryName = "testrepo",
            ProjectName = "testproject",
            OrganizationUrl = "https://dev.azure.com/testorg"
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
                LineNumber = 1
            }
        };

        _mockPullRequestRepository
            .Setup(x => x.GetPullRequestDetailsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(pullRequest);

        _mockPullRequestRepository
            .Setup(x => x.GetPullRequestChangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(fileChanges);

        _mockCodeAnalyzer
            .Setup(x => x.AnalyzeFileAsync(It.IsAny<FileChange>()))
            .ReturnsAsync(codeIssues);

        _mockPullRequestRepository
            .Setup(x => x.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PullRequestComment>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.AnalyzePullRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.IssuesFound.Should().Be(1);
        result.Issues.Should().HaveCount(1);
        result.Issues.First().RuleId.Should().Be("TEST_RULE");
    }

    [Fact]
    public async Task AnalyzePullRequestAsync_WithNoFileChanges_ShouldReturnSuccessWithZeroIssues()
    {
        // Arrange
        var request = new AnalyzePullRequestRequest
        {
            EventType = "git.pullrequest.created",
            OrganizationUrl = "https://dev.azure.com/testorg",
            ProjectName = "testproject",
            RepositoryName = "testrepo",
            PullRequestId = 123,
            PersonalAccessToken = "test-pat"
        };

        var pullRequest = new PullRequest
        {
            PullRequestId = 123,
            Title = "Test PR"
        };

        var emptyFileChanges = new List<FileChange>();

        _mockPullRequestRepository
            .Setup(x => x.GetPullRequestDetailsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(pullRequest);

        _mockPullRequestRepository
            .Setup(x => x.GetPullRequestChangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(emptyFileChanges);

        // Act
        var result = await _service.AnalyzePullRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.IssuesFound.Should().Be(0);
        result.CommentsPosted.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzePullRequestAsync_WithFailedPullRequestFetch_ShouldReturnFailure()
    {
        // Arrange
        var request = new AnalyzePullRequestRequest
        {
            EventType = "git.pullrequest.created",
            OrganizationUrl = "https://dev.azure.com/testorg",
            ProjectName = "testproject",
            RepositoryName = "testrepo",
            PullRequestId = 123,
            PersonalAccessToken = "test-pat"
        };

        _mockPullRequestRepository
            .Setup(x => x.GetPullRequestDetailsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((PullRequest?)null);

        // Act
        var result = await _service.AnalyzePullRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Failed to fetch pull request details");
    }

    [Fact]
    public async Task AnalyzePullRequestAsync_WithException_ShouldReturnFailure()
    {
        // Arrange
        var request = new AnalyzePullRequestRequest
        {
            EventType = "git.pullrequest.created",
            OrganizationUrl = "https://dev.azure.com/testorg",
            ProjectName = "testproject",
            RepositoryName = "testrepo",
            PullRequestId = 123,
            PersonalAccessToken = "test-pat"
        };

        _mockPullRequestRepository
            .Setup(x => x.GetPullRequestDetailsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.AnalyzePullRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Test exception");
    }
}
