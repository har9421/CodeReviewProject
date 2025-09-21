using Newtonsoft.Json;

namespace CodeReviewBot.Models;

public class ResourceContainer
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;
}
