using Newtonsoft.Json;

namespace LlmTornado.Responses.Events;

/// <summary>
/// Event emitted when the input of a custom tool call is finalized.
/// </summary>
public class ResponseEventCustomToolCallInputDone : IResponseAgentEvent
{
    [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)]
    public ResponseAgent? Agent { get; set; }

    /// <summary>
    /// The complete input of the custom tool call.
    /// </summary>
    [JsonProperty("input")]
    public string Input { get; set; } = string.Empty;

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
    /// The type of the event. Always 'response.custom_tool_call_input.done'.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "response.custom_tool_call_input.done";

    /// <summary>
    /// The type of this response event.
    /// </summary>
    [JsonIgnore]
    public ResponseEventTypes EventType => ResponseEventTypes.ResponseCustomToolCallInputDone;
}
