using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses.Events;

/// <summary>Server-assigned steering identity and original target. Failed events also return uncommitted input.</summary>
public class ResponseSteer
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("previous_response_id")] public string PreviousResponseId { get; set; } = string.Empty;
    [JsonProperty("input")] public JToken? Input { get; set; }
}

/// <summary>Extensible steering failure; preserve unknown error codes.</summary>
public class ResponseSteerError
{
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("code")] public string? Code { get; set; }
    [JsonProperty("message")] public string? Message { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Identifies a required client tool result or MCP approval. Supply its result through response.create.</summary>
public class ResponseSteerRequiredInput
{
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("call_id")] public string? CallId { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("execution")] public string? Execution { get; set; }
    [JsonProperty("approval_request_id")] public string? ApprovalRequestId { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

public class ResponseEventSteerAccepted : IResponseEvent
{
    [JsonProperty("type")] public string Type { get; set; } = "response.steer.accepted";
    [JsonProperty("sequence_number")] public int SequenceNumber { get; set; }
    [JsonProperty("stream_id")] public string? StreamId { get; set; }
    [JsonProperty("steer")] public ResponseSteer Steer { get; set; } = new();
    [JsonIgnore] public ResponseEventTypes EventType => ResponseEventTypes.ResponseSteerAccepted;
}

public class ResponseEventSteerPending : IResponseEvent
{
    [JsonProperty("type")] public string Type { get; set; } = "response.steer.pending";
    [JsonProperty("sequence_number")] public int SequenceNumber { get; set; }
    [JsonProperty("stream_id")] public string? StreamId { get; set; }
    [JsonProperty("steer")] public ResponseSteer Steer { get; set; } = new();
    /// <summary>Usually waiting_for_required_input. Unknown reasons are retained.</summary>
    [JsonProperty("reason")] public string? Reason { get; set; }
    [JsonProperty("required_input")] public List<ResponseSteerRequiredInput> RequiredInput { get; set; } = [];
    [JsonIgnore] public ResponseEventTypes EventType => ResponseEventTypes.ResponseSteerPending;
}

public class ResponseEventSteerFailed : IResponseEvent
{
    [JsonProperty("type")] public string Type { get; set; } = "response.steer.failed";
    [JsonProperty("sequence_number")] public int SequenceNumber { get; set; }
    [JsonProperty("stream_id")] public string? StreamId { get; set; }
    [JsonProperty("steer")] public ResponseSteer Steer { get; set; } = new();
    [JsonProperty("error")] public ResponseSteerError Error { get; set; } = new();
    [JsonIgnore] public ResponseEventTypes EventType => ResponseEventTypes.ResponseSteerFailed;
}
