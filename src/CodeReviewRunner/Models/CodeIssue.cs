namespace CodeReviewRunner.Models;

public class CodeIssue
{
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string RuleId { get; set; } = string.Empty;
}