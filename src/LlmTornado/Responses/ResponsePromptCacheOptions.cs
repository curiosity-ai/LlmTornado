using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.Responses;

/// <summary>Minimum cache lifetime for GPT-5.6 and later in the Responses API.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ResponsePromptCacheTtl
{
    /// <summary>Default minimum lifetime; writes cost 1.25 times uncached input.</summary>
    [EnumMember(Value = "30m")]
    ThirtyMinutes,
    /// <summary>Twelve-hour minimum lifetime; writes cost 2.5 times uncached input.</summary>
    [EnumMember(Value = "12h")]
    TwelveHours
}

/// <summary>Controls automatic versus developer-selected cache boundaries.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ResponsePromptCacheMode
{
    /// <summary>Use implicit boundaries as well as any explicit breakpoints.</summary>
    [EnumMember(Value = "implicit")]
    Implicit,
    /// <summary>Only use explicit breakpoints; none means no cache writes.</summary>
    [EnumMember(Value = "explicit")]
    Explicit
}

/// <summary>Request-level prompt caching settings for GPT-5.6 and later.</summary>
public class ResponsePromptCacheOptions
{
    /// <summary>Compare cache reuse against this completed response without loading its conversation.</summary>
    [JsonProperty("comparison_response_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? ComparisonResponseId { get; set; }

    /// <summary>Cache boundary selection. Omission uses implicit mode.</summary>
    [JsonProperty("mode", NullValueHandling = NullValueHandling.Ignore)]
    public ResponsePromptCacheMode? Mode { get; set; }

    /// <summary>Default minimum lifetime for cache writes. Omission uses 30 minutes.</summary>
    [JsonProperty("ttl", NullValueHandling = NullValueHandling.Ignore)]
    public ResponsePromptCacheTtl? Ttl { get; set; }
}

/// <summary>An explicit cache boundary at the end of an input content block.</summary>
public class ResponsePromptCacheBreakpoint
{
    /// <summary>Explicit breakpoint marker.</summary>
    [JsonProperty("mode")]
    public string Mode => "explicit";

    /// <summary>Overrides the request-level minimum lifetime for this prefix.</summary>
    [JsonProperty("ttl", NullValueHandling = NullValueHandling.Ignore)]
    public ResponsePromptCacheTtl? Ttl { get; set; }
}
