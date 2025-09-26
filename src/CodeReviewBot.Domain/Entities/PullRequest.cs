using FluentValidation;

namespace CodeReviewBot.Domain.Entities;

public class PullRequest
{
    public int PullRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceRefName { get; set; } = string.Empty;
    public string TargetRefName { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string OrganizationUrl { get; set; } = string.Empty;
    public List<FileChange> FileChanges { get; set; } = new();
}

public class PullRequestValidator : AbstractValidator<PullRequest>
{
    public PullRequestValidator()
    {
        RuleFor(x => x.PullRequestId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.RepositoryName).NotEmpty();
        RuleFor(x => x.ProjectName).NotEmpty();
        RuleFor(x => x.OrganizationUrl).NotEmpty();
    }
}
