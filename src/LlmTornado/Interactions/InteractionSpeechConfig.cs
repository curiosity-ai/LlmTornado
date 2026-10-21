using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Interactions;

/// <summary>Speaker configuration for Gemini speech synthesis.</summary>
public class InteractionSpeechConfig
{
    /// <summary>Prebuilt voice name, saved voice ID, or stateless replicated voice key.</summary>
    [JsonProperty("voice", NullValueHandling = NullValueHandling.Ignore)]
    public string? Voice { get; set; }

    /// <summary>Speaker name matching the speech_metadata annotation on a text turn.</summary>
    [JsonProperty("speaker", NullValueHandling = NullValueHandling.Ignore)]
    public string? Speaker { get; set; }

    /// <summary>Language of the speech.</summary>
    [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
    public string? Language { get; set; }
}

/// <summary>Text annotation, including speech styling and speaker attribution.</summary>
public class InteractionAnnotation
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("speaker", NullValueHandling = NullValueHandling.Ignore)]
    public string? Speaker { get; set; }

    [JsonProperty("style", NullValueHandling = NullValueHandling.Ignore)]
    public string? Style { get; set; }

    /// <summary>Start byte index of the annotated text segment.</summary>
    [JsonProperty("start_index", NullValueHandling = NullValueHandling.Ignore)]
    public int? StartIndex { get; set; }

    /// <summary>Exclusive end byte index.</summary>
    [JsonProperty("end_index", NullValueHandling = NullValueHandling.Ignore)]
    public int? EndIndex { get; set; }

    /// <summary>Recognized word text for word_info annotations.</summary>
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string? Text { get; set; }

    /// <summary>Word start offset as a Google duration, for example 0.100s.</summary>
    [JsonProperty("start_offset", NullValueHandling = NullValueHandling.Ignore)]
    public string? StartOffset { get; set; }

    /// <summary>Word end offset as a Google duration.</summary>
    [JsonProperty("end_offset", NullValueHandling = NullValueHandling.Ignore)]
    public string? EndOffset { get; set; }

    /// <summary>Additional citation or transcription fields are preserved when replaying content.</summary>
    [JsonExtensionData]
    public Dictionary<string, JToken>? AdditionalData { get; set; }

    /// <summary>Applies speech instructions to a complete text turn.</summary>
    public static InteractionAnnotation Speech(string? style = null, string? speaker = null) => new()
    {
        Type = "speech_metadata", Style = style, Speaker = speaker
    };
}
