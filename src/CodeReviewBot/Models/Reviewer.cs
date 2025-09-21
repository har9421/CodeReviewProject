using Newtonsoft.Json;

namespace CodeReviewBot.Models;

public class Reviewer : IdentityRef
{
    [JsonProperty("vote")]
    public int Vote { get; set; }

    [JsonProperty("hasVoted")]
    public bool HasVoted { get; set; }
}
