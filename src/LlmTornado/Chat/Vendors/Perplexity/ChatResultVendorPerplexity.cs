using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Chat.Vendors.Perplexity;

internal class ChatResultVendorPerplexity : ChatResult
{
    [JsonProperty("usage")]
    public new VendorPerplexityUsage Usage { get; set; }
    
    [JsonProperty("search_results")]
    public List<VendorPerplexitySearchResult>? SearchResults { get; set; }
    
    [JsonProperty("related_questions")]
    public List<string>? RelatedQuestions { get; set; }
    
    [JsonProperty("images")]
    public List<VendorPerplexityImageResult>? Images { get; set; }
    
    public static ChatResult? Deserialize(string json)
    {
        ChatResultVendorPerplexity? resultEx = JsonConvert.DeserializeObject<ChatResultVendorPerplexity>(json);

        if (resultEx is null)
        {
            return null;
        }
        
        ChatResult result = new ChatResult(resultEx)
        {
            Usage = resultEx.Usage is null ? null : new ChatUsage(resultEx.Usage)
        };

        if (resultEx.SearchResults is not null || resultEx.RelatedQuestions is not null || resultEx.Images is not null)
        {
            result.VendorExtensions = new ChatResponseVendorExtensions
            {
                Perplexity = new ChatResponseVendorPerplexityExtensions
                {
                    SearchResults = resultEx.SearchResults,
                    RelatedQuestions = resultEx.RelatedQuestions,
                    Images = resultEx.Images
                }
            };
        }

        return result;
    }
}
