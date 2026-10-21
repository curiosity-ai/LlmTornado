using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses.Events;

/// <summary>
/// Event emitted when an error occurs.
/// </summary>
public class ResponseEventError : IResponseEvent
{
    [OnDeserialized]
    private void ReadNativeError(StreamingContext context)
    {
        Code ??= Error?.Code;
        Param ??= Error?.Param;
        if (string.IsNullOrEmpty(Message)) Message = Error?.Message ?? string.Empty;
    }
    /// <summary>Native WebSocket error details. Legacy HTTP events use the top-level code/message/param fields.</summary>
    [JsonProperty("error")]
    public ResponseWebSocketErrorDetails? Error { get; set; }
    [JsonProperty("status")]
    public int? Status { get; set; }
    [JsonProperty("stream_id")]
    public string? StreamId { get; set; }
    /// <summary>
    /// The error code.
    /// </summary>
    [JsonProperty("code")]
    public string? Code { get; set; }

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The error parameter.
    /// </summary>
    [JsonProperty("param")]
    public string? Param { get; set; }

    /// <summary>
    /// The sequence number of this event.
    /// </summary>
    [JsonProperty("sequence_number")]
    public int SequenceNumber { get; set; }

    /// <summary>
    /// The type of the event. Always 'error'.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "error";

    /// <summary>
    /// The type of this response event.
    /// </summary>
    [JsonIgnore]
    public ResponseEventTypes EventType => ResponseEventTypes.ResponseError;
}

/// <summary>WebSocket protocol error, including server headers such as Retry-After.</summary>
public class ResponseWebSocketErrorDetails
{
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("code")] public string? Code { get; set; }
    [JsonProperty("message")] public string? Message { get; set; }
    [JsonProperty("param")] public string? Param { get; set; }
    [JsonProperty("misalignment")] public LlmTornado.Safety.OpenAiMisalignmentDetails? Misalignment { get; set; }
    [JsonProperty("headers")] public Dictionary<string, string>? Headers { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}
