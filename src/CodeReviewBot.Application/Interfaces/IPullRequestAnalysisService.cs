using CodeReviewBot.Application.DTOs;

namespace CodeReviewBot.Application.Interfaces;

public interface IPullRequestAnalysisService
{
    Task<AnalyzePullRequestResponse> AnalyzePullRequestAsync(AnalyzePullRequestRequest request);
}
