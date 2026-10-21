using Newtonsoft.Json;

namespace LlmTornado.Responses.Events;

/// <summary>
/// Event emitted when there is a delta (partial update) to the reasoning text.
/// </summary>
public class ResponseEventReasoningTextDelta : IResponseAgentEvent
{
    [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)]
    public ResponseAgent? Agent { get; set; }

    /// <summary>
    /// The index of the reasoning content part within the output item.
    /// </summary>
    [JsonProperty("content_index")]
    public int ContentIndex { get; set; }

    /// <summary>
    /// The partial reasoning text.
    /// </summary>
    [JsonProperty("delta")]
    public string Delta { get; set; } = string.Empty;

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
    /// The type of the event. Always 'response.reasoning_text.delta'.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "response.reasoning_text.delta";

    /// <summary>
    /// The type of this response event.
    /// </summary>
    [JsonIgnore]
    public ResponseEventTypes EventType => ResponseEventTypes.ResponseReasoningTextDelta;
}
