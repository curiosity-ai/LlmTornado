using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>
/// Built-in agent toolset (<c>agent_toolset_20260401</c>) for coordinator delegation.
/// </summary>
public class AnthropicManagedAgentToolset
{
    [JsonProperty("default_config", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentToolConfig? DefaultConfig { get; set; }

    [JsonProperty("configs", NullValueHandling = NullValueHandling.Ignore)]
    public List<AnthropicManagedAgentToolConfig>? Configs { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = "agent_toolset_20260401";
}

/// <summary>
/// MCP toolset referencing an MCP server on the agent.
/// </summary>
public class AnthropicManagedAgentMcpToolset
{
    [JsonProperty("default_config", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentToolConfig? DefaultConfig { get; set; }

    [JsonProperty("configs", NullValueHandling = NullValueHandling.Ignore)]
    public List<AnthropicManagedAgentToolConfig>? Configs { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = "mcp_toolset";

    [JsonProperty("mcp_server_name")]
    public string McpServerName { get; set; } = string.Empty;
}

/// <summary>
/// URL MCP server definition on agent create.
/// </summary>
public class AnthropicManagedAgentMcpServer
{
    [JsonProperty("type")]
    public string Type { get; set; } = "url";

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Model configuration for Claude Managed Agents.
/// </summary>
public class AnthropicManagedAgentModelConfig
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("speed", NullValueHandling = NullValueHandling.Ignore)]
    public string? Speed { get; set; }
}

/// <summary>
/// Well-known Claude models for Managed Agents.
/// </summary>
public static class AnthropicManagedAgentModels
{
    public const string ClaudeOpus55 = "claude-opus-5-5";
    public const string ClaudeSonnet55 = "claude-sonnet-5-5";
    public const string ClaudeHaiku55 = "claude-haiku-5-5";
    public const string ClaudeFable51 = "claude-fable-5-1";
    public const string ClaudeOpus5 = "claude-opus-5";
    public const string ClaudeSonnet5 = "claude-sonnet-5";
    public const string ClaudeFable5 = "claude-fable-5";
    public const string ClaudeOpus48 = "claude-opus-4-8";
    public const string ClaudeOpus46 = "claude-opus-4-6";
    public const string ClaudeSonnet46 = "claude-sonnet-4-6";
    public const string ClaudeHaiku45 = "claude-haiku-4-5";
}
