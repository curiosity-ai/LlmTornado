using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Chat.Vendors.Perplexity;

internal class VendorPerplexityChatRequest
{
    public VendorPerplexityChatRequestData? ExtendedRequest { get; set; }
    public ChatRequest?                     NativeRequest   { get; set; }

    [JsonIgnore]
    public ChatRequest SourceRequest { get; set; }

    public string Serialize()
    {
        string serialized = JsonConvert.SerializeObject(ExtendedRequest ?? NativeRequest, EndpointBase.NullSettings);
        return serialized;
    }

    public VendorPerplexityChatRequest(ChatRequest request, IEndpointProvider provider)
    {
        SourceRequest = request;
        ChatRequestVendorPerplexityExtensions? extensions = request.VendorExtensions?.Perplexity;

        if (extensions is not null)
        {
            ExtendedRequest = new VendorPerplexityChatRequestData(request);

            if (extensions.SearchRecencyFilter is not null)
            {
                ExtendedRequest.SearchRecencyFilter = extensions.SearchRecencyFilter;
            }

            if (extensions.SearchBeforeDateFilter is not null)
            {
                ExtendedRequest.SearchBeforeDateFilter = extensions.SearchBeforeDateFilter.Value.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
            }

            if (extensions.SearchAfterDateFilter is not null)
            {
                ExtendedRequest.SearchAfterDateFilter = extensions.SearchAfterDateFilter.Value.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
            }

            if (extensions.ReturnImages is not null)
            {
                ExtendedRequest.ReturnImages = extensions.ReturnImages;
            }

            if (extensions.ReturnRelatedQuestions is not null)
            {
                ExtendedRequest.ReturnRelatedQuestions = extensions.ReturnRelatedQuestions;
            }

            if (extensions.IncludeDomains?.Count > 0 || extensions.ExcludeDomains?.Count > 0)
            {
                List<string> domainList = [];

                if (extensions.IncludeDomains is not null)
                {
                    domainList.AddRange(extensions.IncludeDomains);
                }

                if (extensions.ExcludeDomains is not null)
                {
                    domainList.AddRange(extensions.ExcludeDomains.Select(x => $"-{x}"));
                }

                ExtendedRequest.SearchDomainFilter = domainList;
            }
        }
        else
        {
            NativeRequest = request;
        }
    }
}
/// <summary>
/// https://docs.perplexity.ai/api-reference/chat-completions
/// </summary>
internal class VendorPerplexityChatRequestData : ChatRequest
{
    /// <summary>
    /// %m/%d/%Y
    /// </summary>
    [JsonProperty("search_after_date_filter")]
    public string? SearchAfterDateFilter { get; set; }

    /// <summary>
    /// %m/%d/%Y
    /// </summary>
    [JsonProperty("search_before_date_filter")]
    public string? SearchBeforeDateFilter { get; set; }

    /// <summary>
    /// "week", "day", "month"
    /// </summary>
    [JsonProperty("search_recency_filter")]
    public string? SearchRecencyFilter { get; set; }

    /// <summary>
    /// Determines whether related questions should be returned.
    /// </summary>
    [JsonProperty("return_related_questions")]
    public bool? ReturnRelatedQuestions { get; set; }

    /// <summary>
    /// Determines whether search results should include images.
    /// </summary>
    [JsonProperty("return_images")]
    public bool? ReturnImages { get; set; }

    /// <summary>
    /// The search_domain_filter parameter allows you to control which websites are included in or excluded from the search results used by the Sonar models.
    /// Enabling domain filtering can be done by adding a search_domain_filter field in the request:
    /// "domain1" (include)
    /// "-domain1" (exclude)
    /// </summary>
    [JsonProperty("search_domain_filter ")]
    public List<string>? SearchDomainFilter { get; set; }

    public VendorPerplexityChatRequestData(ChatRequest request) : base(request)
    {

    }
}