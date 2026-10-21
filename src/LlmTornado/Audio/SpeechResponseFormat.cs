using System;
using Newtonsoft.Json;

namespace LlmTornado.Audio;

/// <summary>
///     Audio encodings for speech synthesis.
/// </summary>
public class SpeechResponseFormat
{
    private SpeechResponseFormat(string? value)
    {
        Value = value ?? string.Empty;
    }

    private string Value { get; }

    /// <summary>
    ///     Requests response in mp3 format
    /// </summary>
    public static SpeechResponseFormat Mp3 => new SpeechResponseFormat("mp3");

    /// <summary>
    ///     Requests response in opus format
    /// </summary>
    public static SpeechResponseFormat Opus => new SpeechResponseFormat("opus");

    /// <summary>
    ///     Requests a response in aac format
    /// </summary>
    public static SpeechResponseFormat Aac => new SpeechResponseFormat("aac");

    /// <summary>
    ///     Requests a response in flac format
    /// </summary>
    public static SpeechResponseFormat Flac => new SpeechResponseFormat("flac");

    /// <summary>
    ///     Requests a response in wav format
    /// </summary>
    public static SpeechResponseFormat Wav => new SpeechResponseFormat("wav");

    /// <summary>
    ///     Raw float32 LE samples. Recommended for Mistral Voxtral TTS streaming (lowest latency).
    /// </summary>
    public static SpeechResponseFormat Pcm => new SpeechResponseFormat("pcm");

    /// <summary>
    ///     Requests a response in ogg format
    /// </summary>
    public static SpeechResponseFormat Ogg => new SpeechResponseFormat("ogg");

    /// <summary>
    ///     Requests a response in mulaw format (8-bit G.711 mu-law)
    /// </summary>
    public static SpeechResponseFormat Mulaw => new SpeechResponseFormat("mulaw");

    /// <summary>G.711 A-law audio (supported by Gemini TTS).</summary>
    public static SpeechResponseFormat Alaw => new SpeechResponseFormat("alaw");

    /// <summary>
    ///     Gets the string value for this size to pass to the API
    /// </summary>
    /// <returns>The format as a string</returns>
    public override string ToString()
    {
        return Value;
    }

    /// <summary>
    ///     Gets the string value for this format to pass to the API
    /// </summary>
    /// <param name="value">The SpeechResponseFormat to convert</param>
    public static implicit operator string(SpeechResponseFormat value)
    {
        return value.Value;
    }

    internal class SpeechResponseFormatJsonConverter : JsonConverter<SpeechResponseFormat>
    {
        public override void WriteJson(JsonWriter writer, SpeechResponseFormat value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.ToString());
        }

        public override SpeechResponseFormat ReadJson(JsonReader reader, Type objectType, SpeechResponseFormat existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return new SpeechResponseFormat(reader.ReadAsString());
        }
    }
}
