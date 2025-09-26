using System.Text.Json.Serialization;

namespace CodeReviewBot.Models;

public class PullRequestDetails
{
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public User CreatedBy { get; set; } = new();

    [JsonPropertyName("sourceRefName")]
    public string SourceRefName { get; set; } = string.Empty;

    [JsonPropertyName("targetRefName")]
    public string TargetRefName { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    public Repository Repository { get; set; } = new();
}

public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("uniqueName")]
    public string UniqueName { get; set; } = string.Empty;
}


public class FileChange
{
    public string Path { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string CommitId { get; set; } = string.Empty;
}

public class PullRequestComment
{
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Severity { get; set; } = string.Empty;
}

public class ChangesResponse
{
    [JsonPropertyName("changeEntries")]
    public List<ChangeEntry>? ChangeEntries { get; set; }
}

public class ChangeEntry
{
    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }

    [JsonPropertyName("item")]
    public ChangeItem? Item { get; set; }
}

public class ChangeItem
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("commitId")]
    public string CommitId { get; set; } = string.Empty;
}

public class CodeIssue
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}

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
