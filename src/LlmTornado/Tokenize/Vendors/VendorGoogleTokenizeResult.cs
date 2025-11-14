using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Tokenize.Vendors;

internal class VendorGoogleTokenizeResult : IVendorTokenizeResult
{
    [JsonProperty("totalTokens")]
    public int TotalTokens { get; set; }

    [JsonProperty("cachedContentTokenCount")]
    public int? CachedContentTokenCount { get; set; }

    [JsonProperty("promptTokensDetails")]
    public List<VendorGoogleModalityTokenCount>? PromptTokensDetails { get; set; }

    [JsonProperty("cacheTokensDetails")]
    public List<VendorGoogleModalityTokenCount>? CacheTokensDetails { get; set; }

    public TokenizeResult ToResult()
    {
        return new TokenizeResult
        {
            TotalTokens = TotalTokens,
            NativeResult = this
        };
    }
}

internal class VendorGoogleModalityTokenCount
{
    [JsonProperty("modality")]
    public string? Modality { get; set; }

    [JsonProperty("tokenCount")]
    public int TokenCount { get; set; }
}

