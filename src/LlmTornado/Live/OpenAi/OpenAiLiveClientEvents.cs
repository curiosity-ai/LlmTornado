using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Live.OpenAi;

/// <summary>
/// Base GPT-Live client event. Every outbound JSON message includes <c>type</c>.
/// </summary>
public abstract class OpenAiLiveClientEvent
{
    [JsonProperty("type")]
    public abstract string Type { get; }

    /// <summary>
    /// Optional client-generated id used to correlate <c>error.client_event_id</c> and append acknowledgments.
    /// </summary>
    [JsonProperty("event_id")]
    public string? EventId { get; set; }
}

/// <summary>
/// First WebSocket message on a primary or fork connection. Do not send on sideband or WebRTC data channels.
/// </summary>
public sealed class OpenAiLiveSessionStartEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionStart;

    [JsonProperty("session")]
    public OpenAiLiveSessionConfig Session { get; set; } = new OpenAiLiveSessionConfig();
}

/// <summary>
/// Sparse update of backend delegation and transcript triggers.
/// Delegation is replaced as one complete object.
/// </summary>
public sealed class OpenAiLiveSessionUpdateEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionUpdate;

    [JsonProperty("session")]
    public OpenAiLiveSessionUpdate Session { get; set; } = new OpenAiLiveSessionUpdate();
}

/// <summary>
/// Body of <c>session.update</c>. The delegation type remains fixed after startup.
/// </summary>
public class OpenAiLiveSessionUpdate
{
    [JsonProperty("delegation")]
    public OpenAiLiveDelegationConfig? Delegation { get; set; }

    /// <summary>Omit to keep triggers, send a list to replace them, or an empty list to disable them.</summary>
    [JsonProperty("triggers")]
    public List<OpenAiLiveDecisionTrigger>? Triggers { get; set; }

}

/// <summary>
/// Appends raw base64 audio. Not acknowledged. WebSocket primary only (not WebRTC data channel).
/// </summary>
public sealed class OpenAiLiveInputAudioAppendEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionInputAudioAppend;

    [JsonProperty("audio")]
    public string Audio { get; set; } = string.Empty;
}

/// <summary>
/// Mutes caller input without ending the session.
/// </summary>
public sealed class OpenAiLiveInputAudioMuteEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionInputAudioMute;
}

/// <summary>
/// Resumes caller input after mute.
/// </summary>
public sealed class OpenAiLiveInputAudioUnmuteEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionInputAudioUnmute;
}

/// <summary>
/// Appends trusted instructions, quiet facts, or speakable commentary.
/// </summary>
public abstract class OpenAiLiveContextAppendEvent : OpenAiLiveClientEvent
{
    /// <summary>
    /// Plain-string content, up to 500 tokens.
    /// </summary>
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Required. <c>null</c> for session-wide context; otherwise a known client delegation id.
    /// </summary>
    [JsonProperty("delegation_id", NullValueHandling = NullValueHandling.Include)]
    public string? DelegationId { get; set; }
}

/// <summary>
/// Trusted application instructions. Can interrupt speech in progress.
/// </summary>
public sealed class OpenAiLiveInstructionsAppendEvent : OpenAiLiveContextAppendEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionInstructionsAppend;
}

/// <summary>
/// Factual context the model can use later without speaking immediately.
/// </summary>
public sealed class OpenAiLiveThinkingAppendEvent : OpenAiLiveContextAppendEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionThinkingAppend;
}

/// <summary>
/// Information the model should say aloud (it may paraphrase).
/// </summary>
public sealed class OpenAiLiveCommentaryAppendEvent : OpenAiLiveContextAppendEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionCommentaryAppend;
}

/// <summary>
/// Submits a Responses item (function result or user message). Responses delegation only.
/// </summary>
public sealed class OpenAiLiveResponseItemCreateEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.ResponseItemCreate;

    [JsonProperty("item")]
    public object Item { get; set; } = new JObject();
}

/// <summary>
/// Runs or continues the delegated Responses backend. Responses delegation only.
/// </summary>
public sealed class OpenAiLiveResponseCreateEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.ResponseCreate;
}

/// <summary>
/// Requests graceful shutdown. Keep the transport open until <c>session.closed</c>.
/// </summary>
public sealed class OpenAiLiveSessionCloseEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.SessionClose;
}

/// <summary>
/// Typed Responses items commonly sent through <c>response.item.create</c>.
/// </summary>
public static class OpenAiLiveResponseItems
{
    public static object FunctionCallOutput(string callId, string output)
    {
        return new
        {
            type = "function_call_output",
            call_id = callId,
            output
        };
    }

    public static object UserText(string text)
    {
        return new
        {
            type = "message",
            role = "user",
            content = new[]
            {
                new { type = "input_text", text }
            }
        };
    }

    public static object ImageInput(string imageUrl)
    {
        return new
        {
            type = "message",
            role = "user",
            content = new object[]
            {
                new
                {
                    type = "input_image",
                    image_url = imageUrl
                }
            }
        };
    }
}

/// <summary>
/// Factory helpers for GPT-Live client events.
/// </summary>
public static class OpenAiLiveClientEvents
{
    public static OpenAiLiveSessionStartEvent SessionStart(OpenAiLiveSessionConfig session, string? eventId = null)
    {
        return new OpenAiLiveSessionStartEvent { Session = session, EventId = eventId };
    }

    public static OpenAiLiveSessionUpdateEvent SessionUpdate(OpenAiLiveResponsesDelegation responses, string? eventId = null)
    {
        return new OpenAiLiveSessionUpdateEvent
        {
            EventId = eventId,
            Session = new OpenAiLiveSessionUpdate
            {
                Delegation = OpenAiLiveDelegationConfig.ResponsesBackend(responses)
            }
        };
    }

    public static OpenAiLiveSessionUpdateEvent DecisionsUpdate(OpenAiLiveDecisionsDelegation decisions, string? eventId = null)
        => new OpenAiLiveSessionUpdateEvent
        {
            EventId = eventId,
            Session = new OpenAiLiveSessionUpdate { Delegation = OpenAiLiveDelegationConfig.DecisionsBackend(decisions) }
        };

    public static OpenAiLiveDecisionItemCreateEvent DecisionItemCreate(object item, string? eventId = null)
        => new OpenAiLiveDecisionItemCreateEvent { Item = item, EventId = eventId };

    public static OpenAiLiveInputAudioAppendEvent InputAudioAppend(string base64Audio, string? eventId = null)
    {
        return new OpenAiLiveInputAudioAppendEvent { Audio = base64Audio, EventId = eventId };
    }

    public static OpenAiLiveInputAudioAppendEvent InputAudioAppend(byte[] audio, string? eventId = null)
    {
        return InputAudioAppend(Convert.ToBase64String(audio), eventId);
    }

    public static OpenAiLiveInputAudioMuteEvent InputAudioMute(string? eventId = null)
    {
        return new OpenAiLiveInputAudioMuteEvent { EventId = eventId };
    }

    public static OpenAiLiveInputAudioUnmuteEvent InputAudioUnmute(string? eventId = null)
    {
        return new OpenAiLiveInputAudioUnmuteEvent { EventId = eventId };
    }

    public static OpenAiLiveInstructionsAppendEvent InstructionsAppend(string content, string? delegationId = null, string? eventId = null)
    {
        return new OpenAiLiveInstructionsAppendEvent { Content = content, DelegationId = delegationId, EventId = eventId };
    }

    public static OpenAiLiveThinkingAppendEvent ThinkingAppend(string content, string? delegationId = null, string? eventId = null)
    {
        return new OpenAiLiveThinkingAppendEvent { Content = content, DelegationId = delegationId, EventId = eventId };
    }

    public static OpenAiLiveCommentaryAppendEvent CommentaryAppend(string content, string? delegationId = null, string? eventId = null)
    {
        return new OpenAiLiveCommentaryAppendEvent { Content = content, DelegationId = delegationId, EventId = eventId };
    }

    public static OpenAiLiveResponseItemCreateEvent ResponseItemCreate(object item, string? eventId = null)
    {
        return new OpenAiLiveResponseItemCreateEvent { Item = item, EventId = eventId };
    }

    public static OpenAiLiveResponseCreateEvent ResponseCreate(string? eventId = null)
    {
        return new OpenAiLiveResponseCreateEvent { EventId = eventId };
    }

    public static OpenAiLiveSessionCloseEvent SessionClose(string? eventId = null)
    {
        return new OpenAiLiveSessionCloseEvent { EventId = eventId };
    }
}
