using LlmTornado.Audio.Models;
using System.Threading;
using LlmTornado.Audio.Google;
using LlmTornado.Audio.Models.OpenAi;
using LlmTornado.Models;
using Newtonsoft.Json;

namespace LlmTornado.Audio;

/// <summary>
///     Creates speech request object for tts.
/// </summary>
public class SpeechRequest
{
    /// <summary>Optional cancellation for this speech request.</summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; set; }

    /// <summary>Gemini voice, multi-speaker, styling, and audio configuration.</summary>
    [JsonIgnore]
    public GoogleSpeechOptions? Google { get; set; }

    /// <summary>
    ///     The string to "convert" into speech
    /// </summary>
    [JsonProperty("input")]
    public string Input { get; set; }

    /// <summary>
    ///     ID of the model to use. Tts1 / tts1hd / gpt4o-mini-tts
    /// </summary>
    [JsonProperty("model")]
    [JsonConverter(typeof(AudioModelJsonConverter))]
    public AudioModel Model { get; set; } = AudioModel.OpenAi.Tts.Tts1;

    /// <summary>
    ///     Control the voice of your generated audio with additional instructions. Does not work with tts-1 or tts-1-hd.
    /// </summary>
    [JsonProperty("instructions")]
    public string? Instructions { get; set; }
    
    /// <summary>
    ///     The voice to use for tts
    /// </summary>
    [JsonProperty("voice")]
    [JsonConverter(typeof(SpeechVoice.SpeechVoiceJsonConverter))]
    public SpeechVoice Voice { get; set; } = SpeechVoice.Alloy;

    /// <summary>
    ///     The format of the transcript output, in one of these options: mps, opus, aac, flac
    /// </summary>
    [JsonProperty("response_format")]
    [JsonConverter(typeof(SpeechResponseFormat.SpeechResponseFormatJsonConverter))]
    public SpeechResponseFormat ResponseFormat { get; set; } = SpeechResponseFormat.Mp3;

    /// <summary>
    ///     The speed of tts, must be in range [0.25, 4]. For Groq the range is [0.5, 5].
    /// </summary>
    [JsonProperty("speed")]
    public float? Speed { get; set; } = 1;

    /// <summary>
    ///     The sample rate for generated audio (Groq only). 
    ///     Supported values: 8000, 16000, 22050, 24000, 32000, 44100, 48000.
    ///     Defaults to 48000 if not specified.
    /// </summary>
    [JsonProperty("sample_rate", NullValueHandling = NullValueHandling.Ignore)]
    public int? SampleRate { get; set; }

    /// <summary>
    ///     Saved Mistral voice identifier, or a Gemini prebuilt voice name, saved voice ID, or stateless voice key.
    /// </summary>
    [JsonProperty("voice_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? VoiceId { get; set; }

    /// <summary>
    ///     One-off reference audio (base64) for Mistral Voxtral zero-shot voice cloning.
    /// </summary>
    [JsonProperty("ref_audio", NullValueHandling = NullValueHandling.Ignore)]
    public string? RefAudio { get; set; }
}
