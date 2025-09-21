using Newtonsoft.Json;

namespace CodeReviewBot.Models;

public class PullRequestResource
{
    [JsonProperty("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("createdBy")]
    public IdentityRef? CreatedBy { get; set; }

    [JsonProperty("creationDate")]
    public DateTime CreationDate { get; set; }

    [JsonProperty("repository")]
    public Repository? Repository { get; set; }

    [JsonProperty("sourceRefName")]
    public string SourceRefName { get; set; } = string.Empty;

    [JsonProperty("targetRefName")]
    public string TargetRefName { get; set; } = string.Empty;

    [JsonProperty("mergeStatus")]
    public string MergeStatus { get; set; } = string.Empty;

    [JsonProperty("isDraft")]
    public bool IsDraft { get; set; }

    [JsonProperty("reviewers")]
    public Reviewer[]? Reviewers { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
}
