using FluentValidation;

namespace CodeReviewBot.Domain.Entities;

public class CodingRule
{
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string[]? Languages { get; set; }
    public string? Pattern { get; set; }
    public string[]? AppliesTo { get; set; }
    public string? Suggestion { get; set; }
}

public class CodingRuleValidator : AbstractValidator<CodingRule>
{
    public CodingRuleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Severity).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}
