using Newtonsoft.Json;

namespace LlmTornado.Tokenize.Vendors;

internal class VendorMoonshotAiTokenizeResult : IVendorTokenizeResult
{
    [JsonProperty("data")]
    public VendorMoonshotAiTokenizeData? Data { get; set; }

    public TokenizeResult ToResult()
    {
        return new TokenizeResult
        {
            TotalTokens = Data?.TotalTokens ?? 0,
            NativeResult = this
        };
    }
}

internal class VendorMoonshotAiTokenizeData
{
    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}

