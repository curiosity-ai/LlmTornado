using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Interactions;

/// <summary>Gemini speech recognition configuration.</summary>
public class InteractionTranscriptionConfig
{
    /// <summary>BCP-47 language hints. Omit or leave empty for automatic detection.</summary>
    [JsonProperty("language_codes", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? LanguageCodes { get; set; }

    /// <summary>Up to 1,000 vocabulary phrases. Incompatible with diarization or timestamps.</summary>
    [JsonProperty("custom_vocabulary", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? CustomVocabulary { get; set; }

    /// <summary>Verbatim or smart transcription. Omission uses verbatim.</summary>
    [JsonProperty("mode", NullValueHandling = NullValueHandling.Ignore)]
    public InteractionTranscriptionMode? Mode { get; set; }

    internal void Validate()
    {
        Mode?.Validate();
        if (CustomVocabulary?.Count > 1000)
            throw new ArgumentException("Gemini transcription supports at most 1,000 vocabulary phrases.");
        if (CustomVocabulary is { Count: > 0 } && (Mode?.DiarizationMode is not null || Mode?.TimestampGranularities is { Count: > 0 }))
            throw new ArgumentException("Custom vocabulary cannot be combined with diarization or timestamps.");
    }
}

/// <summary>Discriminated Gemini transcription mode. Both API string and object forms can be read.</summary>
[JsonConverter(typeof(InteractionTranscriptionModeConverter))]
public class InteractionTranscriptionMode
{
    public string Type { get; set; } = "verbatim";

    /// <summary>Set to speaker to return word-level speaker labels. Verbatim mode only.</summary>
    public string? DiarizationMode { get; set; }

    /// <summary>Only word is supported. Verbatim mode only.</summary>
    public List<string>? TimestampGranularities { get; set; }

    public static InteractionTranscriptionMode Smart() => new() { Type = "smart" };

    public static InteractionTranscriptionMode Verbatim(bool diarize = false, bool wordTimestamps = false) => new()
    {
        DiarizationMode = diarize ? "speaker" : null,
        TimestampGranularities = wordTimestamps ? ["word"] : null
    };

    internal void Validate()
    {
        if (Type is not ("smart" or "verbatim")) throw new ArgumentException("Transcription mode must be smart or verbatim.");
        if (Type == "smart" && (DiarizationMode is not null || TimestampGranularities is { Count: > 0 }))
            throw new ArgumentException("Smart transcription does not support diarization or timestamps.");
        if (DiarizationMode is not (null or "speaker")) throw new ArgumentException("Only speaker diarization is supported.");
        if (TimestampGranularities?.Any(x => x != "word") == true) throw new ArgumentException("Gemini transcription supports word timestamps only.");
    }
}

internal sealed class InteractionTranscriptionModeConverter : JsonConverter<InteractionTranscriptionMode>
{
    public override void WriteJson(JsonWriter writer, InteractionTranscriptionMode? value, JsonSerializer serializer)
    {
        if (value is null) { writer.WriteNull(); return; }
        value.Validate();
        writer.WriteStartObject();
        writer.WritePropertyName("type");
        writer.WriteValue(value.Type);
        if (value.DiarizationMode is not null)
        {
            writer.WritePropertyName("diarization_mode");
            writer.WriteValue(value.DiarizationMode);
        }
        if (value.TimestampGranularities is not null)
        {
            writer.WritePropertyName("timestamp_granularities");
            serializer.Serialize(writer, value.TimestampGranularities);
        }
        writer.WriteEndObject();
    }

    public override InteractionTranscriptionMode? ReadJson(JsonReader reader, Type objectType, InteractionTranscriptionMode? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        if (reader.TokenType == JsonToken.String) return new() { Type = (string)reader.Value! };
        JObject obj = JObject.Load(reader);
        return new()
        {
            Type = obj["type"]?.ToString() ?? "verbatim",
            DiarizationMode = obj["diarization_mode"]?.ToString(),
            TimestampGranularities = obj["timestamp_granularities"]?.ToObject<List<string>>(serializer)
        };
    }
}
