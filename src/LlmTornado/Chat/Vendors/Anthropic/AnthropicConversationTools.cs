using System;
using System.Collections.Generic;
using System.Linq;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Chat.Vendors.Anthropic;

/// <summary>A mid-conversation tool addition or removal. Use in a system message after the conversation has started.</summary>
public class AnthropicToolChange
{
    [JsonIgnore]
    public bool IsRemoval { get; set; }

    [JsonProperty("type")]
    public string Type
    {
        get => IsRemoval ? "tool_removal" : "tool_addition";
        set => IsRemoval = value switch
        {
            "tool_removal" => true, "tool_addition" => false,
            _ => throw new ArgumentException("Unknown tool change type.")
        };
    }

    [JsonProperty("tool")]
    public AnthropicInlineTool Tool { get; set; } = new();

    public static AnthropicToolChange Add(AnthropicInlineTool tool) => new() { Tool = tool };
    public static AnthropicToolChange Remove(AnthropicInlineTool tool) => new() { IsRemoval = true, Tool = tool };

    internal void Validate()
    {
        if (IsRemoval && Tool.Type == "tool_definition") throw new ArgumentException("Tool removal requires a reference, not a definition.");
        switch (Tool.Type)
        {
            case "tool_definition" when Tool.Definition is not null: return;
            case "tool_reference" when !string.IsNullOrWhiteSpace(Tool.Name): return;
            case "mcp_tool_reference" when !string.IsNullOrWhiteSpace(Tool.Name) && !string.IsNullOrWhiteSpace(Tool.ServerName): return;
            case "mcp_toolset_reference" when !string.IsNullOrWhiteSpace(Tool.ServerName): return;
            default: throw new ArgumentException("A complete tool definition or named tool reference is required.");
        }
    }
}

/// <summary>A reference to an existing tool, or a full inline definition in Anthropic's native tool schema.</summary>
public class AnthropicInlineTool
{
    [JsonProperty("type")]
    public string Type { get; set; } = "tool_reference";

    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [JsonProperty("server_name", NullValueHandling = NullValueHandling.Ignore)]
    public string? ServerName { get; set; }

    [JsonProperty("definition", NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Definition { get; set; }

    public static AnthropicInlineTool Reference(string name) => new() { Name = name };
    public static AnthropicInlineTool McpTool(string serverName, string name) => new() { Type = "mcp_tool_reference", ServerName = serverName, Name = name };
    public static AnthropicInlineTool McpToolset(string serverName) => new() { Type = "mcp_toolset_reference", ServerName = serverName };

    /// <summary>Defines a function tool, preserving schema, strictness, caching and deferred loading settings.</summary>
    public static AnthropicInlineTool Define(Tool tool) => Define(new VendorAnthropicToolFunction(tool));

    /// <summary>Defines an Anthropic client/server tool or MCP toolset using its native configuration.</summary>
    public static AnthropicInlineTool Define(IVendorAnthropicChatRequestTool tool) => new()
    {
        Type = "tool_definition",
        Definition = JObject.FromObject(tool is IVendorAnthropicChatRequestBuiltInTool builtIn ? new VendorAnthropicToolFunction(builtIn) : tool,
            JsonSerializer.Create(EndpointBase.NullSettings))
    };
}

/// <summary>A fetched MCP tool definition that can be pinned on an MCP toolset.</summary>
public class AnthropicMcpToolDefinition
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("input_schema")]
    public JObject InputSchema { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Server-fetched tool listing. Replay this assistant block unchanged to keep the tool list stable.</summary>
public class AnthropicMcpToolListing
{
    [JsonProperty("type")]
    public string Type => "mcp_tool_listing";

    [JsonProperty("mcp_server_name")]
    public string McpServerName { get; set; } = string.Empty;

    [JsonProperty("tools")]
    public List<AnthropicMcpToolDefinition> Tools { get; set; } = [];
}

/// <summary>Server-reported transformation of a replayed thinking block.</summary>
public class AnthropicInputTransformation
{
    /// <summary>thinking_dropped or thinking_mismatch_allowed. Future values are preserved.</summary>
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Prefix, model, or organization binding mismatch. Future values are preserved.</summary>
    [JsonProperty("reason")]
    public string Reason { get; set; } = string.Empty;
}

internal static class AnthropicConversationTools
{
    internal static IEnumerable<ChatMessagePart> Parts(ChatRequest request) => request.Messages?.SelectMany(x => x.Parts ?? []) ?? [];
    internal static bool HasInlineChanges(ChatRequest request) => Parts(request).Any(x => x.AnthropicToolChange is not null);
    internal static bool DefinesMcpServer(ChatRequest request, string serverName) => Parts(request).Any(x =>
        x.AnthropicToolChange is { IsRemoval: false, Tool.Type: "tool_definition" } change
        && change.Tool.Definition?["type"]?.ToString() == "mcp_toolset" && change.Tool.Definition["mcp_server_name"]?.ToString() == serverName);

    internal static bool UsesMcpListings(ChatRequest request) => request.VendorExtensions?.Anthropic?.EnableMcpToolListing == true
        || request.VendorExtensions?.Anthropic?.McpToolsets?.Any(x => x.Tools is not null) == true
        || Parts(request).Any(x => x.AnthropicMcpToolListing is not null ||
            (x.AnthropicToolChange?.Tool.Definition?["type"]?.ToString() == "mcp_toolset"));
}
