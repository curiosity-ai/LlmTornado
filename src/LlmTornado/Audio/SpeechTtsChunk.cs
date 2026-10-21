namespace LlmTornado.Audio;

/// <summary>A decoded chunk of generated audio. Chunks retain the provider's encoding and contain no added headers.</summary>
public class SpeechTtsChunk
{
    public byte[] Data { get; set; } = [];
    public string? MimeType { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
}
