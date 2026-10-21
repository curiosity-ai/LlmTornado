using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using LlmTornado.Chat.Models;
using LlmTornado.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace LlmTornado.Decision.OpenAi;

/// <summary>Native OpenAI Decisions request: shared evidence and an ordered list of questions.</summary>
public class OpenAiDecisionRequest
{
    /// <summary>Currently gpt-6-luna.</summary>
    [JsonProperty("model")]
    public string Model { get; set; } = ChatModelOpenAiGpt6.ModelV6Luna.Name;

    /// <summary>Text or an ordered list of messages and tool-call/result items.</summary>
    [JsonProperty("input")]
    public object Input { get; set; } = string.Empty;

    /// <summary>Questions evaluated against the shared input, in order.</summary>
    [JsonProperty("questions")]
    public List<OpenAiDecisionQuestion> Questions { get; set; } = [];

    /// <summary>Optional prompt-cache configuration.</summary>
    [JsonProperty("prompt_cache_options", NullValueHandling = NullValueHandling.Ignore)]
    public OpenAiDecisionPromptCacheOptions? PromptCacheOptions { get; set; }

    /// <summary>Optional cache partition key (up to 64 characters).</summary>
    [JsonProperty("prompt_cache_key", NullValueHandling = NullValueHandling.Ignore)]
    public string? PromptCacheKey { get; set; }

    /// <summary>Opaque caller-provided end-user identifier (up to 128 characters).</summary>
    [JsonProperty("safety_identifier", NullValueHandling = NullValueHandling.Ignore)]
    public string? SafetyIdentifier { get; set; }

    /// <summary>Serializes the native Decisions contract.</summary>
    public string Serialize() => JsonConvert.SerializeObject(this, EndpointBase.NullSettings);
}

/// <summary>Shared fields of a native predicate, choice, or score question.</summary>
[JsonConverter(typeof(OpenAiDecisionQuestionConverter))]
public abstract class OpenAiDecisionQuestion
{
    [JsonProperty("type")]
    public abstract string Type { get; }

    /// <summary>Optional unique name echoed on its answer.</summary>
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [JsonProperty("instructions")]
    public string Instructions { get; set; } = string.Empty;
}

/// <summary>Estimates the probability that a condition is true.</summary>
public class OpenAiDecisionPredicate : OpenAiDecisionQuestion
{
    public override string Type => "predicate";
}

/// <summary>Selects one of 2–255 unique string or boolean values.</summary>
public class OpenAiDecisionChoice : OpenAiDecisionQuestion
{
    public override string Type => "choice";

    [JsonProperty("choices")]
    public List<OpenAiDecisionChoiceOption> Choices { get; set; } = [];
}

/// <summary>A typed choice value with an optional rubric description.</summary>
public class OpenAiDecisionChoiceOption
{
    /// <summary>A string or boolean. Boolean true and string "true" are distinct values.</summary>
    [JsonProperty("value")]
    public object Value { get; set; } = string.Empty;

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }
}

/// <summary>Rates evidence over ordered rubric levels.</summary>
public class OpenAiDecisionScore : OpenAiDecisionQuestion
{
    public override string Type => "score";

    [JsonProperty("levels")]
    public List<OpenAiDecisionScoreLevel> Levels { get; set; } = [];
}

/// <summary>One ordered rubric level; its position is its numeric value.</summary>
public class OpenAiDecisionScoreLevel
{
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }
}

internal sealed class OpenAiDecisionQuestionConverter : JsonConverter<OpenAiDecisionQuestion>
{
    public override bool CanWrite => false;

    public override OpenAiDecisionQuestion? ReadJson(JsonReader reader, Type objectType, OpenAiDecisionQuestion? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        JObject obj = JObject.Load(reader);
        OpenAiDecisionQuestion question = obj["type"]?.ToString() switch
        {
            "predicate" => new OpenAiDecisionPredicate(),
            "choice" => new OpenAiDecisionChoice(),
            "score" => new OpenAiDecisionScore(),
            _ => throw new JsonSerializationException("Unknown native OpenAI decision question type.")
        };
        using JsonReader body = obj.CreateReader();
        serializer.Populate(body, question);
        return question;
    }

    public override void WriteJson(JsonWriter writer, OpenAiDecisionQuestion? value, JsonSerializer serializer)
        => throw new NotSupportedException();
}

/// <summary>Native Decisions cache lifetime. This endpoint currently accepts only 30m.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum OpenAiDecisionCacheTtl
{
    [EnumMember(Value = "30m")]
    ThirtyMinutes
}

/// <summary>Native Decisions cache options, with its endpoint-specific lifetime.</summary>
public class OpenAiDecisionPromptCacheOptions
{
    [JsonProperty("mode", NullValueHandling = NullValueHandling.Ignore)]
    public ResponsePromptCacheMode? Mode { get; set; }

    [JsonProperty("ttl", NullValueHandling = NullValueHandling.Ignore)]
    public OpenAiDecisionCacheTtl? Ttl { get; set; }
}
