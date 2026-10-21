using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Threads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>
/// Text block in a user message event.
/// </summary>
public class AnthropicManagedAgentTextBlock
{
    [JsonProperty("type")]
    public string Type { get; set; } = "text";

    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// User message event sent to a session.
/// </summary>
public class AnthropicManagedAgentUserMessageEvent
{
    [JsonProperty("type")]
    public string Type { get; set; } = "user.message";

    [JsonProperty("content")]
    public List<AnthropicManagedAgentTextBlock> Content { get; set; } = [];
}

/// <summary>Appends privileged system context to the primary thread on supported models.
/// Send at most one, last in its batch, immediately after its user message or tool result.</summary>
public class AnthropicManagedAgentSystemMessageEvent
{
    [JsonProperty("type")]
    public string Type => "system.message";
    [JsonProperty("content")]
    public List<AnthropicManagedAgentTextBlock> Content { get; set; } = [];
}

/// <summary>
/// Request body for <c>POST /v1/sessions/{id}/events</c>.
/// </summary>
public class AnthropicManagedAgentSendEventsRequest
{
    [JsonProperty("events")]
    public List<object> Events { get; set; } = [];

    internal string Serialize() => VendorAnthropicManagedAgentsJson.Serialize(this);

    public static AnthropicManagedAgentSendEventsRequest UserMessage(string text) => new()
    {
        Events =
        [
            new AnthropicManagedAgentUserMessageEvent
            {
                Content = [new AnthropicManagedAgentTextBlock { Text = text }]
            }
        ]
    };
}

/// <summary>Events accepted by the session, including persisted IDs and processing timestamps.</summary>
public class AnthropicManagedAgentSentEvents : ApiResultBase
{
    [JsonProperty("data")]
    public List<AnthropicManagedAgentEvent> Data { get; set; } = [];
}

/// <summary>
/// Session event from list or SSE stream.
/// </summary>
public class AnthropicManagedAgentEvent
{
    /// <summary>Upcoming event announced by an event_start preview.</summary>
    [JsonProperty("event")]
    public AnthropicManagedAgentEvent? PreviewEvent { get; set; }

    /// <summary>Authoritative event ID to which an event_delta belongs.</summary>
    [JsonProperty("event_id")]
    public string? EventId { get; set; }

    /// <summary>Best-effort content_delta preview, including its content block index.</summary>
    [JsonProperty("delta")]
    public AnthropicManagedAgentContentDelta? Delta { get; set; }

    [JsonProperty("usage")]
    public AnthropicManagedAgentUsage? Usage { get; set; }

    [JsonProperty("workflow_run_id")]
    public string? WorkflowRunId { get; set; }

    [JsonProperty("workflow_run_phase_id")]
    public string? WorkflowRunPhaseId { get; set; }

    [JsonProperty("phase_started_id")]
    public string? PhaseStartedId { get; set; }

    [JsonProperty("phases")]
    public List<AnthropicManagedAgentWorkflowPhase>? Phases { get; set; }

    [JsonProperty("result")]
    public JToken? Result { get; set; }

    [JsonIgnore]
    public AnthropicManagedAgentWorkflowResult? WorkflowResult => Type == "workflow_run.status_ended" && Result is JObject
        ? Result.ToObject<AnthropicManagedAgentWorkflowResult>() : null;

    [JsonProperty("error")]
    public AnthropicManagedAgentError? Error { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("session_thread_id")]
    public string? SessionThreadId { get; set; }

    [JsonProperty("agent_name")]
    public string? AgentName { get; set; }

    [JsonProperty("from_session_thread_id")]
    public string? FromSessionThreadId { get; set; }

    [JsonProperty("from_agent_name")]
    public string? FromAgentName { get; set; }

    [JsonProperty("to_session_thread_id")]
    public string? ToSessionThreadId { get; set; }

    [JsonProperty("to_agent_name")]
    public string? ToAgentName { get; set; }

    [JsonProperty("input")]
    public JObject? Input { get; set; }

    /// <summary>allow, ask, or deny; null for events without a policy check.</summary>
    [JsonProperty("evaluated_permission")]
    public string? EvaluatedPermission { get; set; }

    [JsonProperty("evaluation")]
    public AnthropicManagedAgentPermissionEvaluation? Evaluation { get; set; }

    [JsonProperty("budget")]
    public AnthropicManagedAgentBudget? Budget { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("processed_at")]
    public string? ProcessedAt { get; set; }

    [JsonProperty("content")]
    public JArray? Content { get; set; }

    [JsonProperty("stop_reason")]
    public JObject? StopReason { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("model_usage")]
    public JObject? ModelUsage { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Parsed SSE event from <c>GET /v1/sessions/{id}/events/stream</c>.
/// </summary>
public class AnthropicManagedAgentStreamEvent
{
    public string? EventType { get; set; }
    public string Data { get; set; } = string.Empty;
    public AnthropicManagedAgentEvent? Event { get; set; }

    internal static AnthropicManagedAgentStreamEvent FromServerSentEvent(ServerSentEvent sse)
    {
        AnthropicManagedAgentStreamEvent evt = new AnthropicManagedAgentStreamEvent
        {
            EventType = sse.EventType,
            Data = sse.Data
        };

        if (string.IsNullOrWhiteSpace(sse.Data))
        {
            return evt;
        }

        try
        {
            evt.Event = JsonConvert.DeserializeObject<AnthropicManagedAgentEvent>(sse.Data, VendorAnthropicManagedAgentsJson.Settings);
            if (evt.EventType is null or "message") evt.EventType = evt.Event?.Type ?? evt.EventType;
        }
        catch
        {
            // keep raw Data for callers that need it
        }

        return evt;
    }
}

/// <summary>
/// Optional callbacks for Managed Agent session SSE streams.
/// </summary>
public class AnthropicManagedAgentStreamEventHandler
{
    /// <summary>Best-effort event_start/event_delta previews. Reconcile with the final event; close abandoned previews on span.model_request_end.</summary>
    public Func<AnthropicManagedAgentEvent, Task>? OnPreview { get; set; }
    /// <summary>All workflow_run lifecycle, phase and error events.</summary>
    public Func<AnthropicManagedAgentEvent, Task>? OnWorkflowRun { get; set; }

    /// <summary>Agent, MCP and custom tool-use events, including permission evaluations where available.</summary>
    public Func<AnthropicManagedAgentEvent, Task>? OnToolUse { get; set; }

    public Func<ServerSentEvent, Task>? OnSse { get; set; }
    public Func<AnthropicManagedAgentStreamEvent, Task>? OnEvent { get; set; }
    public Func<string, Task>? OnTextDelta { get; set; }
    public Func<AnthropicManagedAgentEvent, Task>? OnSessionIdle { get; set; }
}

/// <summary>Incremental preview text. Its index identifies a content block in the final event.</summary>
public class AnthropicManagedAgentContentDelta
{
    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("index")]
    public int? Index { get; set; }
    [JsonProperty("content")]
    public JObject? Content { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JToken>? AdditionalData { get; set; }
}
