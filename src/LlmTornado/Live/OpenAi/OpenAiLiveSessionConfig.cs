using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Live.OpenAi;

/// <summary>
/// GPT-Live session configuration sent in <c>session.start</c>, WebRTC/SIP create, or SIP accept.
/// Unknown fields are rejected by the API at startup; keep this object to documented fields.
/// </summary>
public class OpenAiLiveSessionConfig
{
    /// <summary>
    /// Session type. Required as <c>live</c> when accepting a SIP call.
    /// </summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Voice frontend model. Required at startup (typically <c>gpt-live-1</c>). Immutable after start.
    /// </summary>
    [JsonProperty("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Conversation instructions, up to 16,384 tokens. Immutable after start; append more later.
    /// </summary>
    [JsonProperty("instructions")]
    public string? Instructions { get; set; }

    /// <summary>
    /// Prior text history (max 128 messages / 8,192 tokens). Immutable after start.
    /// </summary>
    [JsonProperty("input")]
    public List<OpenAiLiveInputMessage>? Input { get; set; }

    /// <summary>
    /// Audio format (WebSocket only) and output voice. Omit <c>format</c> for WebRTC and SIP.
    /// </summary>
    [JsonProperty("audio")]
    public OpenAiLiveAudioConfig? Audio { get; set; }

    /// <summary>
    /// Delegation mode. Omitted or <c>null</c> selects client delegation. Immutable after start.
    /// </summary>
    [JsonProperty("delegation")]
    public OpenAiLiveDelegationConfig? Delegation { get; set; }

    /// <summary>Decisions triggers for input/output transcript deltas, independent of delegation.</summary>
    [JsonProperty("triggers")]
    public List<OpenAiLiveDecisionTrigger>? Triggers { get; set; }

    /// <summary>Startup-only WebRTC frontend event permissions. Trusted sideband connections are unaffected.</summary>
    [JsonProperty("client")]
    public OpenAiLiveClientConfig? Client { get; set; }



    /// <summary>
    /// When <c>true</c>, persist the recording so the session can be forked. Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("store")]
    public bool? Store { get; set; }

    /// <summary>
    /// Default WebSocket session: <c>gpt-live-1</c>, Marin, PCM16 24 kHz, client delegation.
    /// </summary>
    public static OpenAiLiveSessionConfig ForWebSocket(
        string? instructions = null,
        OpenAiLiveVoice voice = OpenAiLiveVoice.Marin,
        OpenAiLiveAudioFormat? format = null,
        OpenAiLiveDelegationConfig? delegation = null)
    {
        return new OpenAiLiveSessionConfig
        {
            Model = ChatModelOpenAiRealtime.ModelLive1.Name,
            Instructions = instructions,
            Audio = new OpenAiLiveAudioConfig
            {
                Format = format ?? OpenAiLiveAudioFormat.Pcm24Khz(),
                Output = new OpenAiLiveAudioOutput { Voice = voice }
            },
            Delegation = delegation ?? OpenAiLiveDelegationConfig.Client()
        };
    }

    /// <summary>
    /// WebRTC session config. Audio format is negotiated in SDP — do not set <c>audio.format</c>.
    /// </summary>
    public static OpenAiLiveSessionConfig ForWebRtc(
        string? instructions = null,
        OpenAiLiveVoice voice = OpenAiLiveVoice.Marin,
        OpenAiLiveDelegationConfig? delegation = null)
    {
        return new OpenAiLiveSessionConfig
        {
            Model = ChatModelOpenAiRealtime.ModelLive1.Name,
            Instructions = instructions,
            Audio = new OpenAiLiveAudioConfig
            {
                Output = new OpenAiLiveAudioOutput { Voice = voice }
            },
            Delegation = delegation ?? OpenAiLiveDelegationConfig.Client()
        };
    }

    /// <summary>
    /// SIP accept body. SIP negotiates the codec — omit <c>audio.format</c>. Includes <c>type: live</c>.
    /// </summary>
    public static OpenAiLiveSessionConfig ForSipAccept(
        string? instructions = null,
        OpenAiLiveVoice voice = OpenAiLiveVoice.Marin,
        OpenAiLiveDelegationConfig? delegation = null)
    {
        OpenAiLiveSessionConfig config = ForWebRtc(instructions, voice, delegation);
        config.Type = "live";
        return config;
    }

    /// <summary>
    /// Empty override object for a WebSocket fork that inherits the stored source configuration.
    /// </summary>
    public static OpenAiLiveSessionConfig ForForkInherit()
    {
        return new OpenAiLiveSessionConfig();
    }

    /// <summary>
    /// Sets <see cref="Model"/> from a <see cref="ChatModel"/>.
    /// </summary>
    public OpenAiLiveSessionConfig WithModel(ChatModel model)
    {
        Model = model.GetApiName;
        return this;
    }
}

/// <summary>
/// Public session snapshot returned by <c>session.started</c>, <c>session.updated</c>, and <c>session.closed</c>.
/// </summary>
public class OpenAiLiveSessionResource
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("object")]
    public string? Object { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("expires_at")]
    public long? ExpiresAt { get; set; }

    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("instructions")]
    public string? Instructions { get; set; }

    [JsonProperty("input")]
    public List<OpenAiLiveInputMessage>? Input { get; set; }

    [JsonProperty("audio")]
    public OpenAiLiveAudioConfig? Audio { get; set; }

    [JsonProperty("delegation")]
    public OpenAiLiveDelegationConfig? Delegation { get; set; }

    /// <summary>Decisions triggers for input/output transcript deltas, independent of delegation.</summary>
    [JsonProperty("triggers")]
    public List<OpenAiLiveDecisionTrigger>? Triggers { get; set; }

    /// <summary>Startup-only WebRTC frontend event permissions. Trusted sideband connections are unaffected.</summary>
    [JsonProperty("client")]
    public OpenAiLiveClientConfig? Client { get; set; }



    [JsonProperty("store")]
    public bool? Store { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Combined audio configuration for a GPT-Live session.
/// </summary>
public class OpenAiLiveAudioConfig
{
    /// <summary>
    /// Shared input/output format. WebSocket only. Omit for WebRTC and SIP.
    /// </summary>
    [JsonProperty("format")]
    public OpenAiLiveAudioFormat? Format { get; set; }

    [JsonProperty("output")]
    public OpenAiLiveAudioOutput? Output { get; set; }
}

/// <summary>
/// WebSocket audio format. One codec and rate apply to both directions.
/// </summary>
public class OpenAiLiveAudioFormat
{
    [JsonProperty("type")]
    public OpenAiLiveAudioCodec Type { get; set; } = OpenAiLiveAudioCodec.Pcm;

    [JsonProperty("rate")]
    public int Rate { get; set; } = 24_000;

    public static OpenAiLiveAudioFormat Pcm24Khz() => new OpenAiLiveAudioFormat { Type = OpenAiLiveAudioCodec.Pcm, Rate = 24_000 };

    public static OpenAiLiveAudioFormat Pcm16Khz() => new OpenAiLiveAudioFormat { Type = OpenAiLiveAudioCodec.Pcm, Rate = 16_000 };

    public static OpenAiLiveAudioFormat G711Ulaw() => new OpenAiLiveAudioFormat { Type = OpenAiLiveAudioCodec.Pcmu, Rate = 8_000 };

    public static OpenAiLiveAudioFormat G711Alaw() => new OpenAiLiveAudioFormat { Type = OpenAiLiveAudioCodec.Pcma, Rate = 8_000 };

    /// <summary>Whether this codec requires even-length PCM16 payloads.</summary>
    [JsonIgnore]
    public bool RequiresEvenByteLength => Type == OpenAiLiveAudioCodec.Pcm;
}

/// <summary>
/// Output voice. Immutable after startup.
/// </summary>
public class OpenAiLiveAudioOutput
{
    /// <summary>
    /// Built-in voice. Ignored when <see cref="CustomVoice"/> is set.
    /// </summary>
    [JsonIgnore]
    public OpenAiLiveVoice? Voice { get; set; }

    /// <summary>
    /// Saved custom voice ID. Written as <c>audio.output.voice.id</c>.
    /// </summary>
    [JsonIgnore]
    public string? CustomVoice { get; set; }

    [JsonProperty("voice")]
    [JsonConverter(typeof(OpenAiLiveVoiceJsonConverter))]
    internal OpenAiLiveVoiceWire? VoiceWire
    {
        get
        {
            if (!string.IsNullOrEmpty(CustomVoice))
            {
                return new OpenAiLiveVoiceWire { Value = CustomVoice, IsCustom = true };
            }

            return Voice.HasValue ? new OpenAiLiveVoiceWire { Value = Voice.Value.ToEnumMember() } : null;
        }
        set
        {
            if (value?.Value is null)
            {
                Voice = null;
                CustomVoice = null;
                return;
            }

            if (!value.IsCustom && value.Value.TryParseEnumMember(out OpenAiLiveVoice parsed))
            {
                Voice = parsed;
                CustomVoice = null;
            }
            else
            {
                CustomVoice = value.Value;
                Voice = null;
            }
        }
    }
}

/// <summary>
/// Wire wrapper so <c>voice</c> can be a known enum member or a custom string.
/// </summary>
internal sealed class OpenAiLiveVoiceWire
{
    public bool IsCustom { get; set; }
    public string? Value { get; set; }
}

internal sealed class OpenAiLiveVoiceJsonConverter : JsonConverter<OpenAiLiveVoiceWire?>
{
    public override void WriteJson(JsonWriter writer, OpenAiLiveVoiceWire? value, JsonSerializer serializer)
    {
        if (value?.Value is null)
        {
            writer.WriteNull();
            return;
        }

        if (value.IsCustom)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("id");
            writer.WriteValue(value.Value);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteValue(value.Value);
        }
    }

    public override OpenAiLiveVoiceWire? ReadJson(JsonReader reader, Type objectType, OpenAiLiveVoiceWire? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType is JsonToken.StartObject)
        {
            JObject voice = JObject.Load(reader);
            return new OpenAiLiveVoiceWire { Value = voice["id"]?.ToString(), IsCustom = true };
        }
        return new OpenAiLiveVoiceWire { Value = reader.Value?.ToString() };
    }
}

/// <summary>
/// Seeded history message. Each message has one text part.
/// Developer/user use <c>input_text</c>; assistant uses <c>output_text</c> or <c>text</c>.
/// </summary>
public class OpenAiLiveInputMessage
{
    [JsonProperty("type")]
    public string Type { get; set; } = "message";

    [JsonProperty("role")]
    public OpenAiLiveInputRole Role { get; set; }

    [JsonProperty("content")]
    public List<OpenAiLiveInputContent> Content { get; set; } = [];

    public static OpenAiLiveInputMessage User(string text)
    {
        return new OpenAiLiveInputMessage
        {
            Role = OpenAiLiveInputRole.User,
            Content = [new OpenAiLiveInputContent { Type = "input_text", Text = text }]
        };
    }

    public static OpenAiLiveInputMessage Assistant(string text)
    {
        return new OpenAiLiveInputMessage
        {
            Role = OpenAiLiveInputRole.Assistant,
            Content = [new OpenAiLiveInputContent { Type = "output_text", Text = text }]
        };
    }

    public static OpenAiLiveInputMessage Developer(string text)
    {
        return new OpenAiLiveInputMessage
        {
            Role = OpenAiLiveInputRole.Developer,
            Content = [new OpenAiLiveInputContent { Type = "input_text", Text = text }]
        };
    }
}

/// <summary>
/// Single text part of a seeded history message.
/// </summary>
public class OpenAiLiveInputContent
{
    [JsonProperty("type")]
    public string Type { get; set; } = "input_text";

    [JsonProperty("text")]
    public string? Text { get; set; }
}

/// <summary>
/// Delegation configuration. Mode is chosen at startup and cannot change.
/// </summary>
public class OpenAiLiveDelegationConfig
{
    [JsonProperty("type")]
    public OpenAiLiveDelegationType Type { get; set; } = OpenAiLiveDelegationType.Client;

    /// <summary>
    /// Responses backend settings. Required when <see cref="Type"/> is <see cref="OpenAiLiveDelegationType.Responses"/>.
    /// </summary>
    [JsonProperty("responses")]
    public OpenAiLiveResponsesDelegation? Responses { get; set; }

    /// <summary>Native Decisions backend configuration.</summary>
    [JsonProperty("decisions")]
    public OpenAiLiveDecisionsDelegation? Decisions { get; set; }

    public static OpenAiLiveDelegationConfig DecisionsBackend(OpenAiLiveDecisionsDelegation decisions)
        => new OpenAiLiveDelegationConfig { Type = OpenAiLiveDelegationType.Decisions, Decisions = decisions };


    public static OpenAiLiveDelegationConfig Client()
    {
        return new OpenAiLiveDelegationConfig { Type = OpenAiLiveDelegationType.Client };
    }

    public static OpenAiLiveDelegationConfig ResponsesBackend(OpenAiLiveResponsesDelegation responses)
    {
        return new OpenAiLiveDelegationConfig
        {
            Type = OpenAiLiveDelegationType.Responses,
            Responses = responses
        };
    }

    public static OpenAiLiveDelegationConfig ResponsesBackend(
        string model,
        string? instructions = null,
        IEnumerable<OpenAiLiveDelegationTool>? tools = null)
    {
        return ResponsesBackend(new OpenAiLiveResponsesDelegation
        {
            Model = model,
            Instructions = instructions,
            Tools = tools is null ? null : [..tools]
        });
    }
}

/// <summary>
/// Responses backend used when GPT-Live delegates reasoning and hosted/custom tools.
/// Live supports a subset of the standalone Responses API.
/// </summary>
public class OpenAiLiveResponsesDelegation
{
    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("instructions")]
    public string? Instructions { get; set; }

    [JsonProperty("tools")]
    public List<OpenAiLiveDelegationTool>? Tools { get; set; }

    /// <summary>
    /// <c>auto</c>, <c>required</c>, <c>none</c>, or a named function choice object.
    /// </summary>
    [JsonProperty("tool_choice")]
    public object? ToolChoice { get; set; }

    [JsonProperty("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }

    /// <summary>At least 16 when set.</summary>
    [JsonProperty("max_output_tokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonProperty("service_tier")]
    public ChatRequestServiceTiers? ServiceTier { get; set; }

    [JsonProperty("reasoning")]
    public ReasoningConfiguration? Reasoning { get; set; }

    /// <summary>
    /// Text settings supported by the backend model (e.g. <c>{ "verbosity": "low" }</c>).
    /// </summary>
    [JsonProperty("text")]
    public OpenAiLiveTextConfig? Text { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Backend text configuration. GPT-Live documents <c>verbosity</c>; extra fields are preserved.
/// </summary>
public class OpenAiLiveTextConfig
{
    [JsonProperty("verbosity")]
    public string? Verbosity { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }

    public static OpenAiLiveTextConfig Low() => new OpenAiLiveTextConfig { Verbosity = "low" };

    public static OpenAiLiveTextConfig Medium() => new OpenAiLiveTextConfig { Verbosity = "medium" };

    public static OpenAiLiveTextConfig High() => new OpenAiLiveTextConfig { Verbosity = "high" };
}

/// <summary>
/// A Responses tool entry allowed by GPT-Live (<c>function</c> or <c>web_search</c>).
/// </summary>
public class OpenAiLiveDelegationTool
{
    [JsonProperty("type")]
    public string Type { get; set; } = "function";

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("parameters")]
    public object? Parameters { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }

    public static OpenAiLiveDelegationTool WebSearch()
    {
        return new OpenAiLiveDelegationTool { Type = "web_search" };
    }

    public static OpenAiLiveDelegationTool Function(string name, string? description = null, object? parameters = null)
    {
        return new OpenAiLiveDelegationTool
        {
            Type = "function",
            Name = name,
            Description = description,
            Parameters = parameters
        };
    }

    public static OpenAiLiveDelegationTool FunctionChoice(string name)
    {
        return new OpenAiLiveDelegationTool { Type = "function", Name = name };
    }
}

internal static class OpenAiLiveEnumExtensions
{
    public static string ToEnumMember<T>(this T value) where T : struct, Enum
    {
        MemberInfo[] members = typeof(T).GetMember(value.ToString());
        if (members.Length > 0)
        {
            object[] attrs = members[0].GetCustomAttributes(typeof(EnumMemberAttribute), false);
            if (attrs.Length > 0 && attrs[0] is EnumMemberAttribute member && !string.IsNullOrEmpty(member.Value))
            {
                return member.Value;
            }
        }

        return value.ToString();
    }

    public static bool TryParseEnumMember<T>(this string text, out T value) where T : struct, Enum
    {
        foreach (T item in Enum.GetValues(typeof(T)))
        {
            if (string.Equals(item.ToEnumMember(), text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                value = item;
                return true;
            }
        }

        value = default;
        return false;
    }
}
