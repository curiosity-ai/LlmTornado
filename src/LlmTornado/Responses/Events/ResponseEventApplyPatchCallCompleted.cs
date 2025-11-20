using Newtonsoft.Json;

namespace LlmTornado.Responses.Events;

/// <summary>
/// Event fired when an apply_patch call is completed.
/// </summary>
public class ResponseEventApplyPatchCallCompleted : IResponseEvent
{
    /// <summary>
    /// The type of the event. Always "response.apply_patch_call.completed".
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "response.apply_patch_call.completed";

    /// <summary>
    /// Sequence number of this event.
    /// </summary>
    [JsonProperty("sequence_number")]
    public int SequenceNumber { get; set; }

    /// <summary>
    /// The ID of the associated output item.
    /// </summary>
    [JsonProperty("item_id")]
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// The index of the output item associated with this event.
    /// </summary>
    [JsonProperty("output_index")]
    public int OutputIndex { get; set; }

    /// <summary>
    /// Type-safe enum mapping.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ResponseEventTypes EventType => ResponseEventTypes.ResponseApplyPatchCallCompleted;
}

