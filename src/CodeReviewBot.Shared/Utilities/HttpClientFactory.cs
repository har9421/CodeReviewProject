using Microsoft.Extensions.Logging;
using CodeReviewBot.Shared.Constants;

namespace CodeReviewBot.Shared.Utilities;

public static class HttpClientFactory
{
    public static HttpClient CreateResilientHttpClient(ILogger logger)
    {
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(BotConstants.DefaultTimeoutSeconds);

        return httpClient;
    }
}