using System.Collections.Generic;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>Native confirmation of permanent session deletion.</summary>
public class AnthropicManagedAgentDeletedSession : ApiResultBase
{
    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("id")]
    public string? Id { get; set; }
}

/// <summary>
/// Claude Managed Agent session from <c>POST/GET /v1/sessions</c>.
/// </summary>
public class AnthropicManagedAgentSession : ApiResultBase
{
    [JsonProperty("budget")]
    public AnthropicManagedAgentBudget? Budget { get; set; }

    [JsonIgnore]
    public AnthropicManagedAgentUsage? UsageDetails => Usage?.ToObject<AnthropicManagedAgentUsage>();

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("environment_id")]
    public string? EnvironmentId { get; set; }

    [JsonProperty("agent")]
    public JObject? Agent { get; set; }

    [JsonProperty("resources")]
    public List<JObject>? Resources { get; set; }

    [JsonProperty("vault_ids")]
    public List<string>? VaultIds { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonProperty("usage")]
    public JObject? Usage { get; set; }

    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public string? UpdatedAt { get; set; }

    [JsonProperty("archived_at")]
    public string? ArchivedAt { get; set; }
}

/// <summary>
/// Request body for <c>POST /v1/sessions</c>.
/// </summary>
public class AnthropicManagedAgentSessionCreateRequest
{
    [JsonProperty("budget", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentBudget? Budget { get; set; }

    [JsonProperty("agent")]
    public AnthropicManagedAgentSessionAgent Agent { get; set; } = AnthropicManagedAgentSessionAgent.FromId(string.Empty);

    [JsonProperty("environment_id")]
    public string EnvironmentId { get; set; } = string.Empty;

    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }

    [JsonProperty("resources", NullValueHandling = NullValueHandling.Ignore)]
    public List<object>? Resources { get; set; }

    [JsonProperty("vault_ids", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? VaultIds { get; set; }

    [JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Metadata { get; set; }

    internal string Serialize()
    {
        Budget?.Validate();
        return VendorAnthropicManagedAgentsJson.Serialize(this);
    }
}

/// <summary>
/// Request body for <c>POST /v1/sessions/{id}</c>.
/// </summary>
public class AnthropicManagedAgentSessionUpdateRequest
{
    /// <summary>Changes an existing session budget. Null is omitted unless ClearBudget is true.</summary>
    [JsonProperty("budget", NullValueHandling = NullValueHandling.Include)]
    public AnthropicManagedAgentBudget? Budget { get; set; }

    /// <summary>Sends an explicit null budget to remove an existing cap.</summary>
    [JsonIgnore]
    public bool ClearBudget { get; set; }

    public bool ShouldSerializeBudget() => Budget is not null || ClearBudget;

    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }

    [JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Agent { get; set; }

    [JsonProperty("vault_ids", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? VaultIds { get; set; }

    internal string Serialize()
    {
        if (ClearBudget && Budget is not null) throw new System.ArgumentException("Choose a new budget or ClearBudget, not both.");
        Budget?.Validate();
        return VendorAnthropicManagedAgentsJson.Serialize(this);
    }
}
