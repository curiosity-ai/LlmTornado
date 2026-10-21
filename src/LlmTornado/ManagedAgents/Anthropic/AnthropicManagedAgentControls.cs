using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>Permission policy for server-executed agent or MCP tools. Omission retains the server's default.</summary>
public class AnthropicManagedAgentPermissionPolicy
{
    [JsonProperty("type")]
    public string Type { get; set; } = "auto";
    public static AnthropicManagedAgentPermissionPolicy Auto() => new();
    public static AnthropicManagedAgentPermissionPolicy AlwaysAsk() => new() { Type = "always_ask" };
    public static AnthropicManagedAgentPermissionPolicy AlwaysAllow() => new() { Type = "always_allow" };
}

/// <summary>Toolset defaults or a named per-tool override, including web restrictions.</summary>
public class AnthropicManagedAgentToolConfig
{
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [JsonProperty("enabled", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Enabled { get; set; }

    [JsonProperty("permission_policy", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentPermissionPolicy? PermissionPolicy { get; set; }

    [JsonProperty("allowed_domains", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? AllowedDomains { get; set; }

    [JsonProperty("blocked_domains", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? BlockedDomains { get; set; }

    [JsonProperty("max_content_tokens", NullValueHandling = NullValueHandling.Ignore)]
    public int? MaxContentTokens { get; set; }

    [JsonProperty("user_location", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicManagedAgentUserLocation? UserLocation { get; set; }
}

/// <summary>Approximate location for web search results.</summary>
public class AnthropicManagedAgentUserLocation
{
    [JsonProperty("type")]
    public string Type { get; set; } = "approximate";
    [JsonProperty("city", NullValueHandling = NullValueHandling.Ignore)]
    public string? City { get; set; }
    [JsonProperty("region", NullValueHandling = NullValueHandling.Ignore)]
    public string? Region { get; set; }
    [JsonProperty("country", NullValueHandling = NullValueHandling.Ignore)]
    public string? Country { get; set; }
    [JsonProperty("timezone", NullValueHandling = NullValueHandling.Ignore)]
    public string? Timezone { get; set; }
}

/// <summary>Cloud sandbox networking. Limited hosts also constrain the hosted web tools.</summary>
public class AnthropicManagedAgentNetworking
{
    [JsonProperty("type")]
    public string Type { get; set; } = "limited";

    /// <summary>Exact hosts or *.subdomain patterns, without schemes, ports or paths.</summary>
    [JsonProperty("allowed_hosts", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? AllowedHosts { get; set; }

    [JsonProperty("allow_package_managers", NullValueHandling = NullValueHandling.Ignore)]
    public bool? AllowPackageManagers { get; set; }

    [JsonProperty("allow_mcp_servers", NullValueHandling = NullValueHandling.Ignore)]
    public bool? AllowMcpServers { get; set; }
}

/// <summary>A money amount represented as a string of whole minor units, avoiding floating-point rounding.</summary>
public class AnthropicManagedAgentMoney
{
    /// <summary>Whole cents for USD. For example, 125 means $1.25.</summary>
    [JsonProperty("amount")]
    public string Amount { get; set; } = string.Empty;

    [JsonProperty("currency")]
    public string Currency { get; set; } = "USD";
}

/// <summary>Session budget. The server admits no further model requests after the cap is reached.</summary>
public class AnthropicManagedAgentBudget
{
    [JsonProperty("type")]
    public string Type { get; set; } = "limit";

    [JsonProperty("max_list_cost")]
    public AnthropicManagedAgentMoney MaxListCost { get; set; } = new();

    public static AnthropicManagedAgentBudget UsdCents(long cents)
    {
        if (cents <= 0) throw new ArgumentOutOfRangeException(nameof(cents));
        return new() { MaxListCost = new() { Amount = cents.ToString(CultureInfo.InvariantCulture) } };
    }

    internal void Validate()
    {
        if (Type != "limit" || MaxListCost is null || MaxListCost.Currency != "USD" || MaxListCost.Amount is null || !Regex.IsMatch(MaxListCost.Amount, "^[1-9][0-9]*\\z"))
            throw new ArgumentException("A session budget requires a positive whole number of USD cents without leading zeros.");
    }
}

/// <summary>Typed cost metadata from a session or thread's usage object.</summary>
public class AnthropicManagedAgentUsage
{
    [JsonProperty("list_cost")]
    public AnthropicManagedAgentMoney? ListCost { get; set; }
    [JsonProperty("active_seconds")]
    public double? ActiveSeconds { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>Resolved policy and the server's evaluation under auto.</summary>
public class AnthropicManagedAgentPermissionEvaluation
{
    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("evaluated_permission")]
    public AnthropicManagedAgentEvaluatedPermission? EvaluatedPermission { get; set; }
}

public class AnthropicManagedAgentEvaluatedPermission
{
    /// <summary>allow, ask, or deny. Future values are preserved.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("reason_code")]
    public string? ReasonCode { get; set; }
}
