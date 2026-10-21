using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>Managed Agents uses page/next_page cursors, independently of Messages pagination.</summary>
public class AnthropicManagedAgentPage<T> : ApiResultBase
{
    [JsonProperty("data")]
    public List<T> Data { get; set; } = [];
    [JsonProperty("next_page")]
    public string? NextPage { get; set; }
}

/// <summary>Forward-only pagination for Managed Agents resources.</summary>
public class AnthropicManagedAgentPageQuery
{
    public int? Limit { get; set; }
    public string? Page { get; set; }

    internal virtual IEnumerable<KeyValuePair<string, string>> Parameters()
    {
        if (Limit.HasValue) yield return new("limit", Limit.Value.ToString(CultureInfo.InvariantCulture));
        if (Page is not null) yield return new("page", Page);
    }
}

/// <summary>Event history filters. Types are repeated query parameters; unknown event names are supported.</summary>
public class AnthropicManagedAgentEventQuery : AnthropicManagedAgentPageQuery
{
    public List<string>? Types { get; set; }
    public string? Order { get; set; }
    public string? CreatedAfter { get; set; }
    public string? CreatedAtOrAfter { get; set; }
    public string? CreatedBefore { get; set; }
    public string? CreatedAtOrBefore { get; set; }

    internal override IEnumerable<KeyValuePair<string, string>> Parameters()
    {
        foreach (var pair in base.Parameters()) yield return pair;
        if (Types is not null) foreach (string type in Types) yield return new("types[]", type);
        if (Order is not null) yield return new("order", Order);
        if (CreatedAfter is not null) yield return new("created_at[gt]", CreatedAfter);
        if (CreatedAtOrAfter is not null) yield return new("created_at[gte]", CreatedAtOrAfter);
        if (CreatedBefore is not null) yield return new("created_at[lt]", CreatedBefore);
        if (CreatedAtOrBefore is not null) yield return new("created_at[lte]", CreatedAtOrBefore);
    }
}

/// <summary>Thread pagination and optional running, idle, rescheduling or terminated status filters.</summary>
public class AnthropicManagedAgentThreadQuery : AnthropicManagedAgentPageQuery
{
    public List<string>? Statuses { get; set; }

    internal override IEnumerable<KeyValuePair<string, string>> Parameters()
    {
        foreach (var pair in base.Parameters()) yield return pair;
        if (Statuses is not null) foreach (string status in Statuses) yield return new("statuses[]", status);
    }
}

/// <summary>Opt into best-effort previews. Final events remain authoritative.</summary>
public class AnthropicManagedAgentStreamOptions
{
    /// <summary>agent.message for text fragments; agent.thinking for a start-only signal.</summary>
    public List<string>? EventDeltas { get; set; }

    internal IEnumerable<KeyValuePair<string, string>> Parameters()
    {
        if (EventDeltas is not null) foreach (string type in EventDeltas) yield return new("event_deltas[]", type);
    }
}

/// <summary>Saved agent list filters.</summary>
public class AnthropicManagedAgentListQuery : AnthropicManagedAgentPageQuery
{
    public bool? IncludeArchived { get; set; }
    public string? CreatedAtOrAfter { get; set; }
    public string? CreatedAtOrBefore { get; set; }

    internal override IEnumerable<KeyValuePair<string, string>> Parameters()
    {
        foreach (var pair in base.Parameters()) yield return pair;
        if (IncludeArchived.HasValue) yield return new("include_archived", IncludeArchived.Value ? "true" : "false");
        if (CreatedAtOrAfter is not null) yield return new("created_at[gte]", CreatedAtOrAfter);
        if (CreatedAtOrBefore is not null) yield return new("created_at[lte]", CreatedAtOrBefore);
    }
}

/// <summary>Session list filters with native cursor and repeated status parameters.</summary>
public class AnthropicManagedAgentSessionQuery : AnthropicManagedAgentListQuery
{
    public string? AgentId { get; set; }
    public int? AgentVersion { get; set; }
    public string? DeploymentId { get; set; }
    public string? MemoryStoreId { get; set; }
    public List<string>? Statuses { get; set; }
    public string? Order { get; set; }
    public string? CreatedAfter { get; set; }
    public string? CreatedBefore { get; set; }

    internal override IEnumerable<KeyValuePair<string, string>> Parameters()
    {
        foreach (var pair in base.Parameters()) yield return pair;
        if (AgentId is not null) yield return new("agent_id", AgentId);
        if (AgentVersion.HasValue) yield return new("agent_version", AgentVersion.Value.ToString(CultureInfo.InvariantCulture));
        if (DeploymentId is not null) yield return new("deployment_id", DeploymentId);
        if (MemoryStoreId is not null) yield return new("memory_store_id", MemoryStoreId);
        if (Statuses is not null) foreach (string status in Statuses) yield return new("statuses[]", status);
        if (Order is not null) yield return new("order", Order);
        if (CreatedAfter is not null) yield return new("created_at[gt]", CreatedAfter);
        if (CreatedBefore is not null) yield return new("created_at[lt]", CreatedBefore);
    }
}

internal static class AnthropicManagedAgentUrls
{
    internal static Dictionary<string, object>? LegacyQuery(ListQuery? query, bool order = false)
    {
        if (query is null) return null;
        Dictionary<string, object> parameters = [];
        if (query.Limit.HasValue) parameters["limit"] = query.Limit.Value;
        string? page = query.PageToken ?? query.After;
        if (page is not null) parameters["page"] = page;
        if (order && query.Order.HasValue) parameters["order"] = query.Order == SortOrder.Ascending ? "asc" : "desc";
        return parameters;
    }

    internal static string Resource(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.TrimStart('/').Length == 0)
            throw new ArgumentException("Resource id is required.", nameof(id));
        return "/" + Uri.EscapeDataString(id.TrimStart('/'));
    }

    internal static string Query(string url, IEnumerable<KeyValuePair<string, string>>? parameters)
    {
        if (parameters is null) return url;
        StringBuilder builder = new(url);
        bool hasQuery = url.Contains('?');
        foreach (var pair in parameters)
        {
            builder.Append(hasQuery ? '&' : '?');
            hasQuery = true;
            builder.Append(Uri.EscapeDataString(pair.Key)).Append('=').Append(Uri.EscapeDataString(pair.Value));
        }
        return builder.ToString();
    }
}
