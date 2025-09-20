using Newtonsoft.Json.Linq;

namespace CodeReviewBot.Interfaces;

public interface IWebhookService
{
    Task ProcessWebhookAsync(string eventType, JObject payload, string signature);
    bool ValidateSignature(string jsonPayload, string signatureHeader, string secret);
}