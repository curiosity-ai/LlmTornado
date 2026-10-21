using Newtonsoft.Json;

namespace LlmTornado.Audio;

/// <summary>
///     An event of a speech stream requested with <see cref="SpeechStreamFormats.Sse" />.
/// </summary>
public class SpeechStreamEvent
{
    /// <summary>
    ///     The event type, <c>speech.audio.delta</c> or <c>speech.audio.done</c>.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     A chunk of base64 encoded audio, set on <c>speech.audio.delta</c> events.
    /// </summary>
    [JsonProperty("audio")]
    public string? Audio { get; set; }

    /// <summary>
    ///     Token usage of the request, set on the <c>speech.audio.done</c> event.
    /// </summary>
    [JsonProperty("usage")]
    public SpeechStreamUsage? Usage { get; set; }

    /// <summary>
    ///     The decoded audio of a <c>speech.audio.delta</c> event, empty for other events.
    /// </summary>
    [JsonIgnore]
    public byte[] AudioBytes => string.IsNullOrEmpty(Audio) ? [] : System.Convert.FromBase64String(Audio);
}

/// <summary>
///     Token usage reported at the end of a speech stream.
/// </summary>
public class SpeechStreamUsage
{
    /// <summary>
    ///     Tokens of the input text.
    /// </summary>
    [JsonProperty("input_tokens")]
    public int InputTokens { get; set; }

    /// <summary>
    ///     Tokens of the generated audio.
    /// </summary>
    [JsonProperty("output_tokens")]
    public int OutputTokens { get; set; }

    /// <summary>
    ///     Total tokens.
    /// </summary>
    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}
