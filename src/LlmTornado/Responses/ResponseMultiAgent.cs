using System;
using System.Collections.Generic;
using System.Linq;
using LlmTornado.Responses.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses;

/// <summary>Server-hosted orchestration. The concurrency limit covers all descendants, excluding the root.</summary>
public class ResponseMultiAgentConfiguration
{
    [JsonProperty("enabled")] public bool Enabled { get; set; }
    /// <summary>Omit for the server default of three. The API defines no fixed upper bound.</summary>
    [JsonProperty("max_concurrent_subagents", NullValueHandling = NullValueHandling.Ignore)]
    public int? MaxConcurrentSubagents { get; set; }
}

/// <summary>Options carried in HTTP or WebSocket connection headers, never in the response body.</summary>
public class ResponseApiOptions
{
    public const string MultiAgentBeta = "responses_multi_agent=v1";
    public bool EnableMultiAgent { get; set; }
    public List<string>? Betas { get; set; }

    internal bool HasMultiAgent => EnableMultiAgent || BetaValues().Contains(MultiAgentBeta, StringComparer.Ordinal);
    private IEnumerable<string> BetaValues() => (Betas ?? []).SelectMany(x => x.Split(',')).Select(x => x.Trim()).Where(x => x.Length > 0);
    internal Dictionary<string, object?>? Headers(bool requireMultiAgent = false)
    {
        List<string> values = BetaValues().ToList();
        if (EnableMultiAgent || requireMultiAgent) values.Add(MultiAgentBeta);
        return values.Count == 0 ? null : new() { ["OpenAI-Beta"] = string.Join(",", values.Distinct(StringComparer.Ordinal)) };
    }
    internal static Dictionary<string, object?>? HeadersFor(ResponseRequest request) =>
        (request.ApiOptions ?? new()).Headers(request.RequiresMultiAgentBeta);
}

/// <summary>Agent attribution supplied by the service, such as /root or /root/reviewer.</summary>
public class ResponseAgent
{
    [JsonProperty("agent_name")] public string AgentName { get; set; } = string.Empty;
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

public interface IResponseAgentItem : IResponseOutputItem { ResponseAgent? Agent { get; set; } }
public interface IResponseAgentEvent : IResponseEvent { ResponseAgent? Agent { get; set; } }

/// <summary>Hosted collaboration action. Applications must not execute this as a client function.</summary>
public class ResponseMultiAgentCallItem : IResponseAgentItem
{
    [JsonProperty("type")] public string Type => "multi_agent_call";
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("call_id")] public string CallId { get; set; } = string.Empty;
    /// <summary>spawn_agent, send_message, followup_task, wait_agent, interrupt_agent, list_agents, or a future action.</summary>
    [JsonProperty("action")] public string Action { get; set; } = string.Empty;
    [JsonProperty("arguments")] public string Arguments { get; set; } = string.Empty;
    [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)] public ResponseAgent? Agent { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Result of a hosted collaboration action, linked by call_id.</summary>
public class ResponseMultiAgentCallOutputItem : IResponseAgentItem
{
    [JsonProperty("type")] public string Type => "multi_agent_call_output";
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("call_id")] public string CallId { get; set; } = string.Empty;
    [JsonProperty("action")] public string Action { get; set; } = string.Empty;
    [JsonProperty("output")] public List<ResponseOutputTextContent> Output { get; set; } = [];
    [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)] public ResponseAgent? Agent { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Inter-agent message. The agent attribution identifies the recipient; author identifies the sender.</summary>
public class ResponseAgentMessageItem : IResponseAgentItem
{
    [JsonProperty("type")] public string Type => "agent_message";
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("author")] public string Author { get; set; } = string.Empty;
    [JsonProperty("recipient")] public string Recipient { get; set; } = string.Empty;
    [JsonProperty("content")] public List<ResponseInputContent> Content { get; set; } = [];
    [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)] public ResponseAgent? Agent { get; set; }
    [JsonExtensionData] public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Opaque content used for replay of encrypted agent messages.</summary>
public class ResponseInputContentEncrypted : ResponseInputContent
{
    public override string Type => "encrypted_content";
    [JsonProperty("encrypted_content")] public string EncryptedContent { get; set; } = string.Empty;
}

/// <summary>Replays a hosted collaboration item without treating it as a client tool call.</summary>
public class ResponseMultiAgentHistoryItem : ResponseInputItem
{
    [JsonIgnore] public IResponseAgentItem Item { get; }
    public override string Type => Item.Type;
    public ResponseMultiAgentHistoryItem(IResponseAgentItem item)
    {
        if (item is not (ResponseMultiAgentCallItem or ResponseMultiAgentCallOutputItem or ResponseAgentMessageItem))
            throw new ArgumentException("A multi-agent history item requires a hosted call, hosted result or agent message.", nameof(item));
        Item = item;
        Agent = item.Agent;
    }
}
