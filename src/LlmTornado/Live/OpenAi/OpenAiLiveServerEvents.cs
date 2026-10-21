using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Live.OpenAi;

/// <summary>
/// Base GPT-Live server event. Unknown event types deserialize to this class.
/// </summary>
public class OpenAiLiveServerEvent
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("event_id")]
    public string? EventId { get; set; }

    /// <summary>
    /// Present on command acknowledgments and some errors. Matches the outbound <c>event_id</c>.
    /// </summary>
    [JsonProperty("client_event_id")]
    public string? ClientEventId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }

    /// <summary>
    /// Original JSON payload.
    /// </summary>
    [JsonIgnore]
    public string? RawJson { get; set; }
}

/// <summary>
/// Session reached <c>session.started</c> with the resolved public configuration.
/// </summary>
public sealed class OpenAiLiveSessionStartedEvent : OpenAiLiveServerEvent
{
    [JsonProperty("session")]
    public OpenAiLiveSessionResource? Session { get; set; }
}

/// <summary>
/// Sparse <c>session.update</c> was applied.
/// </summary>
public sealed class OpenAiLiveSessionUpdatedEvent : OpenAiLiveServerEvent
{
    [JsonProperty("session")]
    public OpenAiLiveSessionResource? Session { get; set; }
}

/// <summary>
/// Output audio fragment. Primary WebSocket and reflected sideband.
/// Timing fields are present on some transports and omitted on others.
/// </summary>
public sealed class OpenAiLiveOutputAudioDeltaEvent : OpenAiLiveServerEvent
{
    [JsonProperty("delta")]
    public string? Delta { get; set; }

    [JsonProperty("start_ms")]
    public int? StartMs { get; set; }

    [JsonProperty("end_ms")]
    public int? EndMs { get; set; }

    public byte[] DecodeAudio()
    {
        return string.IsNullOrEmpty(Delta) ? [] : System.Convert.FromBase64String(Delta);
    }
}

/// <summary>
/// Reflected caller audio on a sideband. Payload is PCM16LE 24 kHz regardless of the primary codec.
/// </summary>
public sealed class OpenAiLiveInputAudioReflectedEvent : OpenAiLiveServerEvent
{
    [JsonProperty("audio")]
    public string? Audio { get; set; }

    public byte[] DecodeAudio()
    {
        return string.IsNullOrEmpty(Audio) ? [] : System.Convert.FromBase64String(Audio);
    }
}

/// <summary>
/// Timed transcript fragment. User and assistant streams are independent.
/// </summary>
public class OpenAiLiveTranscriptDeltaEvent : OpenAiLiveServerEvent
{
    [JsonProperty("delta")]
    public string? Delta { get; set; }

    [JsonProperty("start_ms")]
    public int? StartMs { get; set; }

    [JsonProperty("end_ms")]
    public int? EndMs { get; set; }
}

/// <summary>
/// User speech transcript fragment.
/// </summary>
public sealed class OpenAiLiveInputTranscriptDeltaEvent : OpenAiLiveTranscriptDeltaEvent;

/// <summary>
/// Assistant speech transcript fragment.
/// </summary>
public sealed class OpenAiLiveOutputTranscriptDeltaEvent : OpenAiLiveTranscriptDeltaEvent;

/// <summary>
/// Context append was accepted for injection.
/// </summary>
public class OpenAiLiveContextAppendedEvent : OpenAiLiveServerEvent
{
    [JsonProperty("start_ms")]
    public int? StartMs { get; set; }

    [JsonProperty("end_ms")]
    public int? EndMs { get; set; }

    [JsonProperty("delegation_id")]
    public string? DelegationId { get; set; }
}

public sealed class OpenAiLiveInstructionsAppendedEvent : OpenAiLiveContextAppendedEvent;

public sealed class OpenAiLiveThinkingAppendedEvent : OpenAiLiveContextAppendedEvent;

public sealed class OpenAiLiveCommentaryAppendedEvent : OpenAiLiveContextAppendedEvent;

/// <summary>
/// Input mute/unmute was applied.
/// </summary>
public class OpenAiLiveInputAudioMuteAckEvent : OpenAiLiveServerEvent;

public sealed class OpenAiLiveInputAudioMutedEvent : OpenAiLiveInputAudioMuteAckEvent;

public sealed class OpenAiLiveInputAudioUnmutedEvent : OpenAiLiveInputAudioMuteAckEvent;

/// <summary>
/// GPT-Live created a unit of delegated work.
/// </summary>
public sealed class OpenAiLiveDelegationCreatedEvent : OpenAiLiveServerEvent
{
    [JsonProperty("offset_ms")]
    public int? OffsetMs { get; set; }

    [JsonProperty("delegation")]
    public OpenAiLiveDelegationInfo? Delegation { get; set; }
}

/// <summary>
/// Delegation metadata. Does not contain the user's utterance.
/// </summary>
public class OpenAiLiveDelegationInfo
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("target")]
    public string? Target { get; set; }

    /// <summary>
    /// Responses response id when <see cref="Target"/> is <c>responses</c>.
    /// </summary>
    [JsonProperty("response_id")]
    public string? ResponseId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Envelope carrying a nested Responses lifecycle event.
/// Dispatch on <see cref="Event"/>.<c>type</c> and preserve <see cref="DelegationId"/>.
/// </summary>
public sealed class OpenAiLiveResponseEvent : OpenAiLiveServerEvent
{
    [JsonProperty("delegation_id")]
    public string? DelegationId { get; set; }

    [JsonProperty("event")]
    public JObject? Event { get; set; }

    [JsonIgnore]
    public string? NestedType => Event?["type"]?.ToString();
}

/// <summary>
/// Cumulative voice-duration snapshot. Do not sum successive values.
/// </summary>
public sealed class OpenAiLiveUsageUpdatedEvent : OpenAiLiveServerEvent
{
    [JsonProperty("usage")]
    public OpenAiLiveUsage? Usage { get; set; }

    [JsonProperty("context_window")]
    public OpenAiLiveContextWindow? ContextWindow { get; set; }
}

/// <summary>
/// Graceful close completed, including final usage and reason.
/// </summary>
public sealed class OpenAiLiveSessionClosedEvent : OpenAiLiveServerEvent
{
    [JsonProperty("reason")]
    public OpenAiLiveCloseReason? Reason { get; set; }

    [JsonProperty("usage")]
    public OpenAiLiveUsage? Usage { get; set; }

    [JsonProperty("session")]
    public OpenAiLiveSessionResource? Session { get; set; }
}

/// <summary>
/// Cumulative voice duration in seconds.
/// </summary>
public class OpenAiLiveUsage
{
    [JsonProperty("seconds")]
    public double? Seconds { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Context-window occupancy.
/// </summary>
public class OpenAiLiveContextWindow
{
    [JsonProperty("usage_ratio")]
    public double? UsageRatio { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Command, startup, moderation, or delegated Responses error.
/// </summary>
public sealed class OpenAiLiveErrorEvent : OpenAiLiveServerEvent
{
    [JsonProperty("error")]
    public OpenAiLiveErrorPayload? Error { get; set; }
}

/// <summary>
/// Error body. <see cref="ClientEventId"/> is present only when the error correlates to a client command.
/// </summary>
public class OpenAiLiveErrorPayload
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("code")]
    public string? Code { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("param")]
    public string? Param { get; set; }

    [JsonProperty("client_event_id")]
    public string? ClientEventId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// SIP keypad notification. <c>event</c> is one of <c>0</c>–<c>9</c>, <c>*</c>, <c>#</c>, or <c>A</c>–<c>D</c>.
/// </summary>
public class OpenAiLiveDtmfEvent : OpenAiLiveServerEvent
{
    [JsonProperty("event")]
    public string? Event { get; set; }
}

public sealed class OpenAiLiveDtmfReceivedEvent : OpenAiLiveDtmfEvent;

public sealed class OpenAiLiveDtmfSendEvent : OpenAiLiveDtmfEvent;

/// <summary>
/// Parses GPT-Live server JSON into a typed event.
/// </summary>
public static class OpenAiLiveEventParser
{
    public static OpenAiLiveServerEvent Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new OpenAiLiveServerEvent { RawJson = json };
        }

        JObject? obj;
        try
        {
            obj = JObject.Parse(json);
        }
        catch
        {
            return new OpenAiLiveServerEvent { RawJson = json };
        }

        string? type = obj["type"]?.ToString();
        OpenAiLiveServerEvent evt = type switch
        {
            OpenAiLiveEventTypes.SessionStarted => Deserialize<OpenAiLiveSessionStartedEvent>(json),
            OpenAiLiveEventTypes.SessionUpdated => Deserialize<OpenAiLiveSessionUpdatedEvent>(json),
            OpenAiLiveEventTypes.SessionClosed => Deserialize<OpenAiLiveSessionClosedEvent>(json),
            OpenAiLiveEventTypes.SessionOutputAudioDelta => Deserialize<OpenAiLiveOutputAudioDeltaEvent>(json),
            OpenAiLiveEventTypes.SessionInputAudioAppend => Deserialize<OpenAiLiveInputAudioReflectedEvent>(json),
            OpenAiLiveEventTypes.SessionInputTranscriptDelta => Deserialize<OpenAiLiveInputTranscriptDeltaEvent>(json),
            OpenAiLiveEventTypes.SessionOutputTranscriptDelta => Deserialize<OpenAiLiveOutputTranscriptDeltaEvent>(json),
            OpenAiLiveEventTypes.SessionInstructionsAppended => Deserialize<OpenAiLiveInstructionsAppendedEvent>(json),
            OpenAiLiveEventTypes.SessionThinkingAppended => Deserialize<OpenAiLiveThinkingAppendedEvent>(json),
            OpenAiLiveEventTypes.SessionCommentaryAppended => Deserialize<OpenAiLiveCommentaryAppendedEvent>(json),
            OpenAiLiveEventTypes.SessionInputAudioMuted => Deserialize<OpenAiLiveInputAudioMutedEvent>(json),
            OpenAiLiveEventTypes.SessionInputAudioUnmuted => Deserialize<OpenAiLiveInputAudioUnmutedEvent>(json),
            OpenAiLiveEventTypes.SessionDelegationCreated => Deserialize<OpenAiLiveDelegationCreatedEvent>(json),
            OpenAiLiveEventTypes.SessionUsageUpdated => Deserialize<OpenAiLiveUsageUpdatedEvent>(json),
            OpenAiLiveEventTypes.DecisionEvent => Deserialize<OpenAiLiveDecisionEvent>(json),
            OpenAiLiveEventTypes.DecisionItemCreated => Deserialize<OpenAiLiveDecisionItemCreatedEvent>(json),
            OpenAiLiveEventTypes.ResponseEvent => Deserialize<OpenAiLiveResponseEvent>(json),
            OpenAiLiveEventTypes.Error => Deserialize<OpenAiLiveErrorEvent>(json),
            OpenAiLiveEventTypes.TransportDtmfReceived => Deserialize<OpenAiLiveDtmfReceivedEvent>(json),
            OpenAiLiveEventTypes.TransportDtmfSend => Deserialize<OpenAiLiveDtmfSendEvent>(json),
            _ => Deserialize<OpenAiLiveServerEvent>(json)
        };

        evt.RawJson = json;
        evt.Type ??= type;
        return evt;
    }

    private static T Deserialize<T>(string json) where T : OpenAiLiveServerEvent
    {
        return JsonConvert.DeserializeObject<T>(json) ?? (T)(OpenAiLiveServerEvent)new OpenAiLiveServerEvent();
    }
}
