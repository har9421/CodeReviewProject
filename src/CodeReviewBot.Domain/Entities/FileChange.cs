using FluentValidation;

namespace CodeReviewBot.Domain.Entities;

public class FileChange
{
    public string Path { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string CommitId { get; set; } = string.Empty;
}

public class FileChangeValidator : AbstractValidator<FileChange>
{
    public FileChangeValidator()
    {
        RuleFor(x => x.Path).NotEmpty();
        RuleFor(x => x.ChangeType).NotEmpty();
    }
}
