using System.Collections.Generic;
using LlmTornado.Interactions;

namespace LlmTornado.Audio.Google;

/// <summary>Gemini TTS options, used with SpeechRequest.Google.</summary>
public class GoogleSpeechOptions
{
    /// <summary>Prebuilt voice name, saved voice ID, or stateless voice key. Defaults to Kore.</summary>
    public string? Voice { get; set; }

    /// <summary>Language for single-speaker synthesis.</summary>
    public string? Language { get; set; }

    /// <summary>Speaker-to-voice mappings for multi-speaker speech.</summary>
    public List<InteractionSpeechConfig>? Speakers { get; set; }

    /// <summary>Verbatim turns with individual styles and speaker names. Replaces SpeechRequest.Input when supplied.</summary>
    public List<GoogleSpeechTurn>? Turns { get; set; }

    /// <summary>Output bit rate in bits per second for MP3 or Opus.</summary>
    public int? BitRate { get; set; }
}

/// <summary>A verbatim speech turn with optional speaker and delivery instructions.</summary>
public class GoogleSpeechTurn
{
    public string Text { get; set; } = string.Empty;
    public string? Speaker { get; set; }
    public string? Style { get; set; }
}
