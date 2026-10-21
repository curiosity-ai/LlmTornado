using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Safety;

/// <summary>A monitoring alert belonging to the authenticated OpenAI project.</summary>
public class OpenAiSafetyAlert
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("object")] public string Object { get; set; } = "safety.alert";
    [JsonProperty("created_at")] public long CreatedAt { get; set; }
    [JsonProperty("request_id")] public string RequestId { get; set; } = string.Empty;
    [JsonProperty("response_id")] public string ResponseId { get; set; } = string.Empty;
    [JsonProperty("model")] public string Model { get; set; } = string.Empty;
    /// <summary>Block registration succeeded. This does not establish that execution stopped or earlier actions were reversed.</summary>
    [JsonProperty("request_paused")] public bool RequestPaused { get; set; }
    /// <summary>Concern category. Unknown future categories are preserved.</summary>
    [JsonProperty("error_type")] public string ErrorType { get; set; } = string.Empty;
    /// <summary>Customer-safe category description; can be null for Zero Data Retention requests.</summary>
    [JsonProperty("reason")] public string? Reason { get; set; }
    /// <summary>Temporarily available for eligible Zero Data Retention alerts; otherwise omitted or null.</summary>
    [JsonProperty("detailed_explanation")] public string? DetailedExplanation { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Public monitoring metadata on Responses and WebSocket errors. Never causes an automatic retry.</summary>
public class OpenAiMisalignmentDetails
{
    [JsonProperty("error_type")] public string? ErrorType { get; set; }
    [JsonProperty("detailed_explanation")] public string? DetailedExplanation { get; set; }
    /// <summary>Opaque review target; null when unavailable. The application controls any explicit continuation.</summary>
    [JsonProperty("review_target")] public string? ReviewTarget { get; set; }
    [JsonProperty("steer")] public OpenAiMisalignmentSteer? Steer { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

public class OpenAiMisalignmentSteer
{
    [JsonProperty("message")] public string Message { get; set; } = string.Empty;
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Typed error view for HTTP, streamed response failures and WebSocket errors.</summary>
public class OpenAiResponseError
{
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("code")] public string? Code { get; set; }
    [JsonProperty("message")] public string? Message { get; set; }
    [JsonProperty("param")] public string? Param { get; set; }
    [JsonProperty("misalignment")] public OpenAiMisalignmentDetails? Misalignment { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
    [JsonIgnore] public bool IsMisalignmentPolicyViolation => Code == "misalignment_policy_violation";

    /// <summary>Reads an API error object or an envelope containing error. The original transport result is unchanged.</summary>
    public static OpenAiResponseError? Parse(string json)
    {
        JToken token = JToken.Parse(json);
        if (token is not JObject root) return null;
        return (root["error"] ?? root) is JObject error ? error.ToObject<OpenAiResponseError>() : null;
    }
}
