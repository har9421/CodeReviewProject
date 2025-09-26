using FluentValidation;

namespace CodeReviewBot.Application.DTOs;

public class AnalyzePullRequestRequest
{
    public string EventType { get; set; } = string.Empty;
    public string OrganizationUrl { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public int PullRequestId { get; set; }
    public string PersonalAccessToken { get; set; } = string.Empty;
}

public class AnalyzePullRequestRequestValidator : AbstractValidator<AnalyzePullRequestRequest>
{
    public AnalyzePullRequestRequestValidator()
    {
        RuleFor(x => x.EventType).NotEmpty();
        RuleFor(x => x.OrganizationUrl).NotEmpty();
        RuleFor(x => x.ProjectName).NotEmpty();
        RuleFor(x => x.RepositoryName).NotEmpty();
        RuleFor(x => x.PullRequestId).GreaterThan(0);
        RuleFor(x => x.PersonalAccessToken).NotEmpty();
    }
}
