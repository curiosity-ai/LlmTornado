using System.Collections.Generic;
using System.Globalization;
using LlmTornado;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Audio;

/// <summary>
///     Transcription format for vtt results.
/// </summary>
public class TranscriptionResult : ApiResultBase
{
    /// <summary>
    ///     Text of the transcript result.
    /// </summary>
    public string Text { get; set; }
    
    /// <summary>
    ///     Task type. Translate or transcript.
    /// </summary>
    [JsonProperty("task")]
    public string? Task { get; set; }

    /// <summary>
    ///     Language of the audio.
    /// </summary>
    [JsonProperty("language")]
    public string? Language { get; set; }

    /// <summary>
    ///     Audio duration.
    /// </summary>
    [JsonProperty("duration")]
    public float Duration { get; set; }
    
    /// <summary>
    ///     The log probabilities of the tokens in the transcription. Only returned with the models gpt-4o-transcribe and gpt-4o-mini-transcribe if logprobs is added to the include array.
    /// </summary>
    [JsonProperty("logprobs")]
    public List<TranscriptionLogprob>? Logprobs { get; set; }

    /// <summary>
    ///     Audio segments.
    /// </summary>
    [JsonProperty("segments")]
    public List<TranscriptionSegment> Segments { get; set; } = [];
    
    /// <summary>
    ///     Usage statistics.
    /// </summary>
    [JsonProperty("usage")]
    public TranscriptionUsage? Usage { get; set; }
    
    /// <summary>
    ///     Audio words.
    /// </summary>
    [JsonProperty("words")]
    public List<TranscriptionWord> Words { get; set; } = [];

    [JsonIgnore]
    internal AudioStreamEventTypes? EventType { get; set; }
    
    /// <summary>
    /// Debug view of the result.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return Text;
    }
}

/// <summary>
/// The log probabilities of the tokens in the transcription. Only returned with the models gpt-4o-transcribe and gpt-4o-mini-transcribe if logprobs is added to the include array.
/// </summary>
public class TranscriptionLogprob
{
    /// <summary>
    /// The bytes of the token.
    /// </summary>
    [JsonProperty("bytes")]
    public byte[] Bytes { get; set; }
    
    /// <summary>
    /// The log probability of the token.
    /// </summary>
    [JsonProperty("logprob")]
    public float Logprob { get; set; }
    
    /// <summary>
    /// The token in the transcription.
    /// </summary>
    [JsonProperty("token")]
    public string Token { get; set; }
    
    /// <summary>
    /// Debug serialization of the logprob.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{Logprob.ToString("F8", CultureInfo.InvariantCulture)}: {Token}";
    }
}

/// <summary>
///     Word of a transcript.
/// </summary>
public class TranscriptionWord
{
    /// <summary>
    /// End time.
    /// </summary>
    public float End { get; set; }
    
    /// <summary>
    /// Start time.
    /// </summary>
    public float Start { get; set; }
    
    /// <summary>
    /// Word.
    /// </summary>
    public string Word { get; set; }
    
    /// <summary>
    /// Debug serialization of the transcription word.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{Start.ToString("F2", CultureInfo.InvariantCulture)}-{End.ToString("F2", CultureInfo.InvariantCulture)}: {Word}";
    }
}

/// <summary>
///     Segment of the transcript.
/// </summary>
public class TranscriptionSegment
{
    /// <summary>
    ///     Segment id
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; }
    
    /// <summary>
    ///     Speaker label.
    /// </summary>
    [JsonProperty("speaker")]
    public string? Speaker { get; set; }

    /// <summary>
    ///     Start time.
    /// </summary>
    [JsonProperty("start")]
    public float Start { get; set; }

    /// <summary>
    ///     End time.
    /// </summary>
    [JsonProperty("end")]
    public float End { get; set; }

    /// <summary>
    ///     Text content of the segment.
    /// </summary>
    [JsonProperty("text")]
    public string Text { get; set; }

    /// <summary>
    ///     Text tokens.
    /// </summary>
    [JsonProperty("tokens")]
    public int[] Tokens { get; set; }

    /// <summary>
    ///     Temperature parameter used for generating the segment.
    /// </summary>
    [JsonProperty("temperature")]
    public double Temperature { get; set; }

    /// <summary>
    ///     Average log probabilities of the text.
    /// </summary>
    [JsonProperty("avg_logprob")]
    public double AvgLogProb { get; set; }

    /// <summary>
    ///     Compression ratio of the segment. If the value is greater than 2.4, consider the compression failed.
    /// </summary>
    [JsonProperty("compression_ratio")]
    public double CompressionRation { get; set; }

    /// <summary>
    ///     Probability of no speech in the segment. If the value is higher than 1.0 and the avg_logprob is below -1, consider this segment silent.
    /// </summary>
    [JsonProperty("no_speech_prob")]
    public double NoSpeechProb { get; set; }

    /// <summary>
    ///     Transient.
    /// </summary>
    [JsonProperty("transient")]
    public bool Transient { get; set; }
    
    /// <summary>
    ///     Seek offset of the segment.
    /// </summary>
    [JsonProperty("seek")]
    public int Seek { get; set; }

    /// <summary>
    /// Debug serialization of the transcription segment.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{Start.ToString("F2", CultureInfo.InvariantCulture)}-{End.ToString("F2", CultureInfo.InvariantCulture)}: {Text}";
    }
}

/// <summary>
/// Usage statistics for transcription.
/// </summary>
public class TranscriptionUsage
{
    /// <summary>
    /// The type of the usage object (duration or tokens).
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; }
    
    /// <summary>
    /// Duration of the input audio in seconds.
    /// </summary>
    [JsonProperty("seconds")]
    public float? Seconds { get; set; }
    
    /// <summary>
    /// Number of input tokens billed for this request.
    /// </summary>
    [JsonProperty("input_tokens")]
    public int? InputTokens { get; set; }
    
    /// <summary>
    /// Number of output tokens generated.
    /// </summary>
    [JsonProperty("output_tokens")]
    public int? OutputTokens { get; set; }
    
    /// <summary>
    /// Total number of tokens used (input + output).
    /// </summary>
    [JsonProperty("total_tokens")]
    public int? TotalTokens { get; set; }
    
    /// <summary>
    /// Details about the input tokens billed for this request.
    /// </summary>
    [JsonProperty("input_token_details")]
    public TranscriptionUsageInputTokenDetails? InputTokenDetails { get; set; }
}

/// <summary>
/// Details about the input tokens billed for this request.
/// </summary>
public class TranscriptionUsageInputTokenDetails
{
    /// <summary>
    /// Text tokens.
    /// </summary>
    [JsonProperty("text_tokens")]
    public int? TextTokens { get; set; }
    
    /// <summary>
    /// Audio tokens.
    /// </summary>
    [JsonProperty("audio_tokens")]
    public int? AudioTokens { get; set; }
}
