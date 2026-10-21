using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Webhooks;

/// <summary>OpenAI project webhook envelope. The event ID and the resource ID in data.id are distinct.</summary>
public class OpenAiWebhookEvent
{
    [JsonProperty("object")] public string Object { get; set; } = "event";
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("created_at")] public long CreatedAt { get; set; }
    [JsonProperty("data")] public OpenAiWebhookEventData Data { get; set; } = new();
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
    [JsonIgnore] public bool IsSafetyAlertCreated => Type == "safety.alert.created";
    /// <summary>Parses JSON when the caller has already verified the delivery signature.</summary>
    public static OpenAiWebhookEvent? ParseUnverified(string payload) => JsonConvert.DeserializeObject<OpenAiWebhookEvent>(payload);
}

public class OpenAiWebhookEventData
{
    /// <summary>The resource ID to retrieve, such as an alert ID for safety.alert.created.</summary>
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}
