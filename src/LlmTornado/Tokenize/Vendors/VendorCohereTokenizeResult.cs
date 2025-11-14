using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Tokenize.Vendors;

internal class VendorCohereTokenizeResult : IVendorTokenizeResult
{
    [JsonProperty("tokens")]
    public List<int> Tokens { get; set; } = [];

    [JsonProperty("token_strings")]
    public List<string> TokenStrings { get; set; } = [];

    public TokenizeResult ToResult()
    {
        return new TokenizeResult
        {
            TotalTokens = Tokens?.Count ?? 0,
            NativeResult = this
        };
    }
}

