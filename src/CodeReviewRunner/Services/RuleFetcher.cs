using Newtonsoft.Json.Linq;

namespace CodeReviewRunner.Services;

public class RuleFetcher
{
    private readonly HttpClient _http = new();

    public async Task<JObject> FetchAsync(string url)
    {
        var json = await _http.GetStringAsync(url);
        return JObject.Parse(json);
    }
}