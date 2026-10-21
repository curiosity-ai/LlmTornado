using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses.Events;

public class ResponseEventInjectCreated : IResponseEvent
{
    [JsonProperty("type")] public string Type { get; set; } = "response.inject.created";
    [JsonProperty("sequence_number")] public int SequenceNumber { get; set; }
    [JsonProperty("stream_id")] public string? StreamId { get; set; }
    [JsonProperty("response_id")] public string ResponseId { get; set; } = string.Empty;
    [JsonIgnore] public ResponseEventTypes EventType => ResponseEventTypes.ResponseInjectCreated;
}

public class ResponseEventInjectFailed : IResponseEvent
{
    [JsonProperty("type")] public string Type { get; set; } = "response.inject.failed";
    [JsonProperty("sequence_number")] public int SequenceNumber { get; set; }
    [JsonProperty("stream_id")] public string? StreamId { get; set; }
    [JsonProperty("response_id")] public string ResponseId { get; set; } = string.Empty;
    /// <summary>Original, uncommitted input. Preserve for an explicit response.create continuation when appropriate.</summary>
    [JsonProperty("input")] public JArray Input { get; set; } = [];
    [JsonProperty("error")] public ResponseInjectError Error { get; set; } = new();
    [JsonIgnore] public ResponseEventTypes EventType => ResponseEventTypes.ResponseInjectFailed;
}

public class ResponseInjectError
{
    [JsonProperty("code")] public string? Code { get; set; }
    [JsonProperty("message")] public string? Message { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}
