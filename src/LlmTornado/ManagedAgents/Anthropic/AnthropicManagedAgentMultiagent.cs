using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>
/// Multiagent configuration on agent create/update. The existing coordinator shape remains supported.
/// </summary>
public class AnthropicManagedAgentMultiagentConfig
{
    /// <summary>
    /// coordinator or multiagent_20261001. Use Dynamic() for workflows and inline agents.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "coordinator";

    /// <summary>
    /// Agents the coordinator may delegate to (1–20 entries).
    /// </summary>
    [JsonProperty("agents")]
    public List<AnthropicManagedAgentRosterEntry> Agents { get; set; } = [];

    public bool ShouldSerializeAgents() => Type == "coordinator";

    [JsonProperty("subagents", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentDelegationConfig? Subagents { get; set; }

    [JsonProperty("workflows", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentDelegationConfig? Workflows { get; set; }

    [JsonProperty("advisor", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentAdvisorConfig? Advisor { get; set; }

    /// <summary>Current multiagent configuration. Omitted settings retain server defaults or stored values on updates.</summary>
    public static AnthropicManagedAgentMultiagentConfig Dynamic() => new() { Type = "multiagent_20261001" };
}

/// <summary>
/// Resolved multiagent topology on an agent resource.
/// </summary>
public class AnthropicManagedAgentMultiagent
{
    [JsonProperty("subagents")]
    public AnthropicManagedAgentDelegationConfig? Subagents { get; set; }

    [JsonProperty("workflows")]
    public AnthropicManagedAgentDelegationConfig? Workflows { get; set; }

    [JsonProperty("advisor")]
    public AnthropicManagedAgentAdvisorConfig? Advisor { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("agents")]
    public List<AnthropicManagedAgentReference>? Agents { get; set; }
}

/// <summary>
/// Roster entry when creating a coordinator: agent ID, versioned reference, or self.
/// </summary>
[JsonConverter(typeof(AnthropicManagedAgentRosterEntryConverter))]
public class AnthropicManagedAgentRosterEntry
{
    /// <summary>
    /// When set, serializes as a plain agent ID string.
    /// </summary>
    [JsonIgnore]
    public string? AgentId { get; set; }

    /// <summary>
    /// <c>agent</c>, <c>self</c>, or omitted when using <see cref="AgentId"/>.
    /// </summary>
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string? Id { get; set; }

    [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
    public int? Version { get; set; }

    public static AnthropicManagedAgentRosterEntry FromAgentId(string agentId) => new() { AgentId = agentId };

    public static AnthropicManagedAgentRosterEntry FromAgent(string agentId, int? version = null) => new()
    {
        Type = "agent",
        Id = agentId,
        Version = version
    };

    public static AnthropicManagedAgentRosterEntry Self { get; } = new() { Type = "self" };
}

/// <summary>
/// Resolved agent reference in a coordinator roster.
/// </summary>
public class AnthropicManagedAgentReference
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("version")]
    public int? Version { get; set; }
}

internal sealed class AnthropicManagedAgentRosterEntryConverter : JsonConverter<AnthropicManagedAgentRosterEntry>
{
    public override AnthropicManagedAgentRosterEntry? ReadJson(JsonReader reader, Type objectType, AnthropicManagedAgentRosterEntry? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            return AnthropicManagedAgentRosterEntry.FromAgentId(reader.Value?.ToString() ?? string.Empty);
        }

        if (reader.TokenType == JsonToken.Null) return null;
        JObject obj = JObject.Load(reader);
        return new() { Type = (string?)obj["type"], Id = (string?)obj["id"], Version = (int?)obj["version"] };
    }

    public override void WriteJson(JsonWriter writer, AnthropicManagedAgentRosterEntry? value, JsonSerializer serializer)
    {
        if (value is null) { writer.WriteNull(); return; }
        if (value?.AgentId is not null)
        {
            writer.WriteValue(value.AgentId);
            return;
        }

        writer.WriteStartObject();
        if (value?.Type is not null)
        {
            writer.WritePropertyName("type");
            writer.WriteValue(value.Type);
        }

        if (value?.Id is not null)
        {
            writer.WritePropertyName("id");
            writer.WriteValue(value.Id);
        }

        if (value?.Version is not null)
        {
            writer.WritePropertyName("version");
            writer.WriteValue(value.Version);
        }

        writer.WriteEndObject();
    }
}

/// <summary>Workflow or subagent settings. Predefined and inline agents are configured independently.</summary>
public class AnthropicManagedAgentDelegationConfig
{
    [JsonProperty("type")]
    public string Type { get; set; } = "enabled";

    [JsonProperty("inline_agents", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentToggle? InlineAgents { get; set; }

    [JsonProperty("predefined_agents", NullValueHandling = NullValueHandling.Ignore)]
    public List<AnthropicManagedAgentRosterEntry>? PredefinedAgents { get; set; }
}

/// <summary>Explicit enabled/disabled setting.</summary>
public class AnthropicManagedAgentToggle
{
    [JsonProperty("type")]
    public string Type { get; set; } = "enabled";
    public static AnthropicManagedAgentToggle Enabled() => new();
    public static AnthropicManagedAgentToggle Disabled() => new() { Type = "disabled" };
}

/// <summary>Advisor settings for the primary thread. Managed Agents accepts type and model only.</summary>
public class AnthropicManagedAgentAdvisorConfig
{
    [JsonProperty("type")]
    public string Type { get; set; } = "enabled";

    [JsonProperty("model", NullValueHandling = NullValueHandling.Ignore)]
    public string? Model { get; set; }
}
