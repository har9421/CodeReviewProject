using CodeReviewBot.Application.DTOs;
using CodeReviewBot.Application.Interfaces;
using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeReviewBot.Application.Services;

public class PullRequestAnalysisService : IPullRequestAnalysisService
{
    private readonly IPullRequestRepository _pullRequestRepository;
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly ILogger<PullRequestAnalysisService> _logger;

    public PullRequestAnalysisService(
        IPullRequestRepository pullRequestRepository,
        ICodeAnalyzer codeAnalyzer,
        ILogger<PullRequestAnalysisService> logger)
    {
        _pullRequestRepository = pullRequestRepository;
        _codeAnalyzer = codeAnalyzer;
        _logger = logger;
    }

    public async Task<AnalyzePullRequestResponse> AnalyzePullRequestAsync(AnalyzePullRequestRequest request)
    {
        try
        {
            _logger.LogInformation("Starting analysis for PR {PullRequestId}", request.PullRequestId);

            // 1. Get pull request details
            var pullRequest = await _pullRequestRepository.GetPullRequestDetailsAsync(
                request.OrganizationUrl, request.ProjectName, request.RepositoryName,
                request.PullRequestId, request.PersonalAccessToken);

            if (pullRequest == null)
            {
                return new AnalyzePullRequestResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to fetch pull request details"
                };
            }

            // 2. Get file changes
            var fileChanges = await _pullRequestRepository.GetPullRequestChangesAsync(
                request.OrganizationUrl, request.ProjectName, request.RepositoryName,
                request.PullRequestId, request.PersonalAccessToken);

            if (!fileChanges.Any())
            {
                _logger.LogInformation("No C# files changed in PR {PullRequestId}", request.PullRequestId);
                return new AnalyzePullRequestResponse
                {
                    Success = true,
                    IssuesFound = 0,
                    CommentsPosted = 0
                };
            }

            // 3. Analyze each changed file
            var allIssues = new List<CodeIssue>();
            foreach (var fileChange in fileChanges)
            {
                var issues = await _codeAnalyzer.AnalyzeFileAsync(fileChange);
                allIssues.AddRange(issues);
            }

            _logger.LogInformation("Found {IssueCount} issues across {FileCount} files in PR {PullRequestId}",
                allIssues.Count, fileChanges.Count, request.PullRequestId);

            // 4. Post comments for issues (limit to avoid spam)
            var commentCount = 0;
            var maxComments = 50; // This should come from configuration

            foreach (var issue in allIssues.Take(maxComments))
            {
                if (commentCount >= maxComments)
                {
                    _logger.LogInformation("Reached maximum comment limit ({MaxComments}) for PR {PullRequestId}",
                        maxComments, request.PullRequestId);
                    break;
                }

                var comment = new PullRequestComment
                {
                    Content = $"🤖 **Code Review Bot**\n\n**{issue.Severity}**: {issue.Message}\n\n" +
                              (!string.IsNullOrEmpty(issue.Suggestion) ? $"💡 **Suggestion**: {issue.Suggestion}\n\n" : "") +
                              $"📋 **Rule**: {issue.RuleId}",
                    FilePath = issue.FilePath,
                    LineNumber = issue.LineNumber,
                    Severity = issue.Severity
                };

                var success = await _pullRequestRepository.PostCommentAsync(
                    request.OrganizationUrl, request.ProjectName, request.RepositoryName,
                    request.PullRequestId, request.PersonalAccessToken, comment);

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
                    Content = $"🤖 **Code Review Bot Analysis Summary**\n\n" +
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

                await _pullRequestRepository.PostCommentAsync(
                    request.OrganizationUrl, request.ProjectName, request.RepositoryName,
                    request.PullRequestId, request.PersonalAccessToken, summaryComment);

                _logger.LogInformation("Posted summary comment for PR {PullRequestId}", request.PullRequestId);
            }

            return new AnalyzePullRequestResponse
            {
                Success = true,
                IssuesFound = allIssues.Count,
                CommentsPosted = commentCount,
                Issues = allIssues.Select(i => new CodeIssueDto
                {
                    RuleId = i.RuleId,
                    Severity = i.Severity,
                    Message = i.Message,
                    Suggestion = i.Suggestion,
                    FilePath = i.FilePath,
                    LineNumber = i.LineNumber
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing pull request {PullRequestId}", request.PullRequestId);
            return new AnalyzePullRequestResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
