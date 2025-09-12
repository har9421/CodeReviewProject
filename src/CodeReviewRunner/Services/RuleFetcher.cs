using Newtonsoft.Json.Linq;

namespace CodeReviewRunner.Services;

public class RuleFetcher
{
    private readonly HttpClient _http = new();

    public async Task<JObject> FetchAsync(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var json = await _http.GetStringAsync(url);
            return JObject.Parse(json);
        }

        if (File.Exists(url))
        {
            var json = await File.ReadAllTextAsync(url);
            return JObject.Parse(json);
        }

        throw new ArgumentException($"Rules location not found: {url}");
    }
}