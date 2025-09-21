using Newtonsoft.Json;

namespace CodeReviewBot.Models;

public class WebhookPayload<TResource>
{
    [JsonProperty("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonProperty("resource")]
    public TResource? Resource { get; set; }

    [JsonProperty("resourceVersion")]
    public string ResourceVersion { get; set; } = string.Empty;

    [JsonProperty("resourceContainers")]
    public ResourceContainers? ResourceContainers { get; set; }
}
