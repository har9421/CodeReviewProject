using Newtonsoft.Json;

namespace CodeReviewBot.Models;

public class ResourceContainers
{
    [JsonProperty("collection")]
    public ResourceContainer? Collection { get; set; }

    [JsonProperty("project")]
    public ResourceContainer? Project { get; set; }

    [JsonProperty("repository")]
    public ResourceContainer? Repository { get; set; }
}
