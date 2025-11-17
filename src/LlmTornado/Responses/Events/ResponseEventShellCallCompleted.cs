using Newtonsoft.Json;

namespace LlmTornado.Responses.Events;

/// <summary>
/// Event fired when a shell call is completed.
/// </summary>
public class ResponseEventShellCallCompleted : IResponseEvent
{
    /// <summary>
    /// The type of the event. Always "response.shell_call.completed".
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "response.shell_call.completed";

    /// <summary>
    /// Sequence number of this event.
    /// </summary>
    [JsonProperty("sequence_number")]
    public int SequenceNumber { get; set; }

    /// <summary>
    /// ID of the associated output item.
    /// </summary>
    [JsonProperty("item_id")]
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// Output index that triggered the event.
    /// </summary>
    [JsonProperty("output_index")]
    public int OutputIndex { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public ResponseEventTypes EventType => ResponseEventTypes.ResponseShellCallCompleted;
}

