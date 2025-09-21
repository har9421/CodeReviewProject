using Newtonsoft.Json;

namespace CodeReviewBot.Models;

public class Project
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("state")]
    public string State { get; set; } = string.Empty;

    [JsonProperty("visibility")]
    public string Visibility { get; set; } = string.Empty;
}
