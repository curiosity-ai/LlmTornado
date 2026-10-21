using System.Collections.Generic;
using LlmTornado.Chat.Models;
using LlmTornado.Decision.OpenAi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Live.OpenAi;

/// <summary>Native Decisions delegation settings. Live supplies the conversation transcript.</summary>
public class OpenAiLiveDecisionsDelegation
{
    [JsonProperty("model")]
    public string? Model { get; set; } = ChatModelOpenAiGpt6.ModelV6Luna.Name;

    [JsonProperty("questions")]
    public List<OpenAiDecisionQuestion>? Questions { get; set; }
}

/// <summary>Independent Decisions invocation on an input or output transcript delta.</summary>
public class OpenAiLiveDecisionTrigger
{
    [JsonProperty("type")]
    public string Type => "decisions";

    /// <summary>session.input_transcript.delta or session.output_transcript.delta.</summary>
    [JsonProperty("event_type")]
    public string EventType { get; set; } = OpenAiLiveEventTypes.SessionInputTranscriptDelta;

    /// <summary>Live supplies the model and trigger ID.</summary>
    [JsonProperty("decisions")]
    public OpenAiLiveDecisionTriggerConfig Decisions { get; set; } = new OpenAiLiveDecisionTriggerConfig();
}

/// <summary>Questions evaluated against a transcript and shared Decisions items.</summary>
public class OpenAiLiveDecisionTriggerConfig
{
    [JsonProperty("questions")]
    public List<OpenAiDecisionQuestion> Questions { get; set; } = [];
}

/// <summary>Adds a message or tool result to shared Decisions context.</summary>
public sealed class OpenAiLiveDecisionItemCreateEvent : OpenAiLiveClientEvent
{
    public override string Type => OpenAiLiveEventTypes.DecisionItemCreate;

    [JsonProperty("item")]
    public object Item { get; set; } = new JObject();
}

/// <summary>Acknowledges a shared Decisions item.</summary>
public sealed class OpenAiLiveDecisionItemCreatedEvent : OpenAiLiveServerEvent
{
    [JsonProperty("item")]
    public JObject? Item { get; set; }
}

/// <summary>A native Decisions lifecycle event produced by delegation or a transcript trigger.</summary>
public sealed class OpenAiLiveDecisionEvent : OpenAiLiveServerEvent
{
    [JsonProperty("delegation_id")]
    public string? DelegationId { get; set; }

    [JsonProperty("trigger_id")]
    public string? TriggerId { get; set; }

    [JsonProperty("source_event_type")]
    public string? SourceEventType { get; set; }

    [JsonProperty("source_event_id")]
    public string? SourceEventId { get; set; }

    [JsonProperty("event")]
    public OpenAiLiveDecisionLifecycleEvent? Event { get; set; }
}

/// <summary>Nested decision event. Check Type before reading the completed decision.</summary>
public class OpenAiLiveDecisionLifecycleEvent
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("decision")]
    public OpenAiDecisionResult? Decision { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}
