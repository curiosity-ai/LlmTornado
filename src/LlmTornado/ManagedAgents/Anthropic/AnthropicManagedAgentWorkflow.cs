using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>Phase declared in workflow_run.created. Phase event IDs can also name undeclared phases.</summary>
public class AnthropicManagedAgentWorkflowPhase
{
    [JsonProperty("id")]
    public string? Id { get; set; }
    [JsonProperty("name")]
    public string? Name { get; set; }
    [JsonProperty("description")]
    public string? Description { get; set; }
}

/// <summary>Result of a workflow run. Completed means execution ended, not that every thread succeeded.</summary>
public class AnthropicManagedAgentWorkflowResult
{
    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("error")]
    public AnthropicManagedAgentError? Error { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JToken>? AdditionalData { get; set; }
}

public class AnthropicManagedAgentError
{
    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("message")]
    public string? Message { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Common settlement and interrupt events sent to a session.</summary>
public class AnthropicManagedAgentToolConfirmationEvent
{
    [JsonProperty("type")]
    public string Type => "user.tool_confirmation";
    [JsonProperty("tool_use_id")]
    public string ToolUseId { get; set; } = string.Empty;
    [JsonProperty("result")]
    public string Result { get; set; } = "deny";
    [JsonProperty("deny_message", NullValueHandling = NullValueHandling.Ignore)]
    public string? DenyMessage { get; set; }
}

public class AnthropicManagedAgentCustomToolResultEvent
{
    [JsonProperty("type")]
    public string Type => "user.custom_tool_result";
    [JsonProperty("custom_tool_use_id")]
    public string CustomToolUseId { get; set; } = string.Empty;
    [JsonProperty("content")]
    public List<AnthropicManagedAgentTextBlock> Content { get; set; } = [];
    [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsError { get; set; }
}

public class AnthropicManagedAgentInterruptEvent
{
    [JsonProperty("type")]
    public string Type => "user.interrupt";
    [JsonProperty("session_thread_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? SessionThreadId { get; set; }
}
