using Newtonsoft.Json;

namespace LlmTornado.Responses.Events;

/// <summary>
/// Event emitted when the JSON arguments of an MCP tool call are finalized, sent as a string.
/// </summary>
public class ResponseEventMcpCallArgumentsTextDone : IResponseAgentEvent
{
    [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)]
    public ResponseAgent? Agent { get; set; }

    /// <summary>
    /// The finalized arguments as a JSON string.
    /// </summary>
    [JsonProperty("arguments")]
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier of the item the event belongs to.
    /// </summary>
    [JsonProperty("item_id")]
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// The index of the output item in the response's output array.
    /// </summary>
    [JsonProperty("output_index")]
    public int OutputIndex { get; set; }

    /// <summary>
    /// The sequence number of this event, used to order streaming events.
    /// </summary>
    [JsonProperty("sequence_number")]
    public int SequenceNumber { get; set; }

    /// <summary>
    /// The type of the event. Always 'response.mcp_call_arguments.done'.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "response.mcp_call_arguments.done";

    /// <summary>
    /// The type of this response event.
    /// </summary>
    [JsonIgnore]
    public ResponseEventTypes EventType => ResponseEventTypes.ResponseMcpCallArgumentsTextDone;
}
