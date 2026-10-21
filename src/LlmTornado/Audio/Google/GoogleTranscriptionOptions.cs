using LlmTornado.Interactions;

namespace LlmTornado.Audio.Google;

/// <summary>Native Gemini transcription settings.</summary>
public class GoogleTranscriptionOptions
{
    /// <summary>MIME type of a file URI, for example audio/wav. Inline AudioFile inputs infer this from ContentType.</summary>
    public string? MimeType { get; set; }

    /// <summary>Language, vocabulary, and verbatim/smart transcription controls.</summary>
    public InteractionTranscriptionConfig? Configuration { get; set; }
}
