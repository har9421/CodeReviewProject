using Newtonsoft.Json;

namespace CodeReviewBot.Models;

public class Repository
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("project")]
    public Project? Project { get; set; }
}
