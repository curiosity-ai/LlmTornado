using Newtonsoft.Json;

namespace LlmTornado.Responses;

/// <summary>Prompt-cache comparison outcome, also returned on the response.completed streaming snapshot.</summary>
public class ResponsePromptCacheDiagnostics
{
    /// <summary>cache_hit, cache_miss, comparison_response_not_found, or unavailable.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>Classified miss reason, such as tools_changed or input_changed. May be absent.</summary>
    [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
    public string? Reason { get; set; }

    /// <summary>Estimated reusable prefix length from the comparison response.</summary>
    [JsonProperty("comparison_reusable_tokens", NullValueHandling = NullValueHandling.Ignore)]
    public int? ComparisonReusableTokens { get; set; }

    /// <summary>Estimated number of comparison tokens that were not reused. Use usage for billing counts.</summary>
    [JsonProperty("cache_missed_tokens", NullValueHandling = NullValueHandling.Ignore)]
    public int? CacheMissedTokens { get; set; }
}
