using FluentValidation;

namespace CodeReviewBot.Domain.Entities;

public class CodeIssue
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}

public class CodeIssueValidator : AbstractValidator<CodeIssue>
{
    public CodeIssueValidator()
    {
        RuleFor(x => x.FilePath).NotEmpty();
        RuleFor(x => x.LineNumber).GreaterThan(0);
        RuleFor(x => x.Severity).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
        RuleFor(x => x.RuleId).NotEmpty();
    }
}
