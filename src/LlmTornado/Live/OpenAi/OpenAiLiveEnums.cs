using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.Live.OpenAi;

/// <summary>
/// How a GPT-Live WebSocket is attached to a session.
/// </summary>
public enum OpenAiLiveConnectKind
{
    /// <summary>Primary audio+events socket at <c>/v1/live/sessions</c>.</summary>
    Primary,

    /// <summary>Sideband control socket at <c>/v1/live/sessions/{id}/attach</c>. Do not send <c>session.start</c>.</summary>
    Sideband,

    /// <summary>Fork socket at <c>/v1/live/sessions/{id}/fork</c>. Send <c>session.start</c> with inherited or override settings.</summary>
    Fork
}

/// <summary>
/// GPT-Live voices. Default is <see cref="Marin"/>.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum OpenAiLiveVoice
{
    [EnumMember(Value = "marin")]
    Marin,

    [EnumMember(Value = "quartz")]
    Quartz,

    [EnumMember(Value = "ripple")]
    Ripple,

    [EnumMember(Value = "vesper")]
    Vesper,

    [EnumMember(Value = "willow")]
    Willow,

    [EnumMember(Value = "stone")]
    Stone,

    [EnumMember(Value = "gleam")]
    Gleam,

    [EnumMember(Value = "meridian")]
    Meridian,

    [EnumMember(Value = "bossa")]
    Bossa,

    [EnumMember(Value = "tempo")]
    Tempo,

    [EnumMember(Value = "beacon")]
    Beacon,

    [EnumMember(Value = "delta")]
    Delta,

    [EnumMember(Value = "cinder")]
    Cinder,

    [EnumMember(Value = "nuri")]
    Nuri,

    [EnumMember(Value = "juni")]
    Juni,

    [EnumMember(Value = "flitz")]
    Flitz,

    [EnumMember(Value = "brise")]
    Brise,

    [EnumMember(Value = "sillage")]
    Sillage,

    [EnumMember(Value = "nira")]
    Nira,

    [EnumMember(Value = "harema")]
    Harema,

    [EnumMember(Value = "shida")]
    Shida,

    [EnumMember(Value = "noeul")]
    Noeul,

    [EnumMember(Value = "alloy")]
    Alloy,

    [EnumMember(Value = "ash")]
    Ash,

    [EnumMember(Value = "ballad")]
    Ballad,

    [EnumMember(Value = "cedar")]
    Cedar,

    [EnumMember(Value = "coral")]
    Coral,

    [EnumMember(Value = "echo")]
    Echo,

    [EnumMember(Value = "sage")]
    Sage,

    [EnumMember(Value = "shimmer")]
    Shimmer,

    [EnumMember(Value = "verse")]
    Verse,
}

/// <summary>
/// Shared WebSocket audio codec. One format applies to both input and output for the session.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum OpenAiLiveAudioCodec
{
    /// <summary>Mono signed 16-bit little-endian PCM.</summary>
    [EnumMember(Value = "audio/pcm")]
    Pcm,

    /// <summary>G.711 μ-law, 8 kHz, one byte per sample.</summary>
    [EnumMember(Value = "audio/pcmu")]
    Pcmu,

    /// <summary>G.711 A-law, 8 kHz, one byte per sample.</summary>
    [EnumMember(Value = "audio/pcma")]
    Pcma
}

/// <summary>
/// Who owns delegated reasoning and tool work.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum OpenAiLiveDelegationType
{
    /// <summary>Your application runs the backend and returns results to GPT-Live.</summary>
    [EnumMember(Value = "client")]
    Client,

    /// <summary>GPT-Live calls a configured Responses model and tools.</summary>
    [EnumMember(Value = "responses")]
    Responses,

    /// <summary>GPT-Live calls native Decisions directly.</summary>
    [EnumMember(Value = "decisions")]
    Decisions
}

/// <summary>
/// Why a GPT-Live session finalized.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum OpenAiLiveCloseReason
{
    [EnumMember(Value = "close_requested")]
    CloseRequested,

    [EnumMember(Value = "expired")]
    Expired,

    [EnumMember(Value = "content")]
    Content,

    [EnumMember(Value = "remote_hangup")]
    RemoteHangup,

    [EnumMember(Value = "connection_lost")]
    ConnectionLost
}

/// <summary>
/// Role of a seeded GPT-Live history message.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum OpenAiLiveInputRole
{
    [EnumMember(Value = "developer")]
    Developer,

    [EnumMember(Value = "user")]
    User,

    [EnumMember(Value = "assistant")]
    Assistant
}

/// <summary>
/// Official GPT-Live client and server event type names.
/// </summary>
public static class OpenAiLiveEventTypes
{
    public const string SessionStart = "session.start";
    public const string SessionUpdate = "session.update";
    public const string SessionClose = "session.close";
    public const string SessionInputAudioAppend = "session.input_audio.append";
    public const string SessionInputAudioMute = "session.input_audio.mute";
    public const string SessionInputAudioUnmute = "session.input_audio.unmute";
    public const string SessionInstructionsAppend = "session.instructions.append";
    public const string SessionThinkingAppend = "session.thinking.append";
    public const string SessionCommentaryAppend = "session.commentary.append";
    public const string ResponseItemCreate = "response.item.create";
    public const string ResponseCreate = "response.create";
    public const string DecisionItemCreate = "decision.item.create";
    public const string DecisionItemCreated = "decision.item.created";
    public const string DecisionEvent = "decision.event";

    public const string SessionStarted = "session.started";
    public const string SessionUpdated = "session.updated";
    public const string SessionClosed = "session.closed";
    public const string SessionOutputAudioDelta = "session.output_audio.delta";
    public const string SessionInputTranscriptDelta = "session.input_transcript.delta";
    public const string SessionOutputTranscriptDelta = "session.output_transcript.delta";
    public const string SessionInstructionsAppended = "session.instructions.appended";
    public const string SessionThinkingAppended = "session.thinking.appended";
    public const string SessionCommentaryAppended = "session.commentary.appended";
    public const string SessionInputAudioMuted = "session.input_audio.muted";
    public const string SessionInputAudioUnmuted = "session.input_audio.unmuted";
    public const string SessionDelegationCreated = "session.delegation.created";
    public const string SessionUsageUpdated = "session.usage.updated";
    public const string ResponseEvent = "response.event";
    public const string Error = "error";
    public const string TransportDtmfReceived = "transport.dtmf.received";
    public const string TransportDtmfSend = "transport.dtmf.send";

    /// <summary>WebRTC data-channel label used by official GPT-Live browser examples.</summary>
    public const string WebRtcDataChannelLabel = "oai-events";

    /// <summary>Current inbound SIP webhook name.</summary>
    public const string WebhookTransportIncoming = "live.transport.incoming";

    /// <summary>Deprecated inbound SIP webhook name. Still delivered during migration.</summary>
    public const string WebhookCallIncoming = "live.call.incoming";
}
