namespace CodeReviewBot.Application.DTOs;

public class AnalyzePullRequestResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int IssuesFound { get; set; }
    public int CommentsPosted { get; set; }
    public List<CodeIssueDto> Issues { get; set; } = new();
}

public class CodeIssueDto
{
    public string RuleId { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}
