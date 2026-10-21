using System.Collections.Generic;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>Primary, subagent, workflow or advisor execution thread within a session.</summary>
public class AnthropicManagedAgentThread : ApiResultBase
{
    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("id")]
    public string? Id { get; set; }
    [JsonProperty("session_id")]
    public string? SessionId { get; set; }
    [JsonProperty("parent_thread_id")]
    public string? ParentThreadId { get; set; }
    [JsonProperty("workflow_run_id")]
    public string? WorkflowRunId { get; set; }
    [JsonProperty("status")]
    public string? Status { get; set; }
    /// <summary>Resolved agent or reserved advisor configuration. All native fields are preserved.</summary>
    [JsonProperty("agent")]
    public JObject? Agent { get; set; }
    [JsonProperty("stats")]
    public JObject? Stats { get; set; }
    [JsonProperty("usage")]
    public JObject? Usage { get; set; }
    [JsonIgnore]
    public AnthropicManagedAgentUsage? UsageDetails => Usage?.ToObject<AnthropicManagedAgentUsage>();
    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }
    [JsonProperty("updated_at")]
    public string? UpdatedAt { get; set; }
    [JsonProperty("archived_at")]
    public string? ArchivedAt { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JToken>? AdditionalData { get; set; }
}
