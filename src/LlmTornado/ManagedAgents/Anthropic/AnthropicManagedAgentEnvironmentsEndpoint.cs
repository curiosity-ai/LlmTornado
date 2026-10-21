using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>
/// Claude Managed Agent environments — sandbox templates required before starting sessions.
/// </summary>
public class AnthropicManagedAgentEnvironmentsEndpoint : EndpointBase
{
    internal AnthropicManagedAgentEnvironmentsEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.AnthropicManagedAgentEnvironments;

    /// <summary>
    /// Creates an environment (<c>POST /v1/environments</c>).
    /// </summary>
    public Task<HttpCallResult<AnthropicManagedAgentEnvironment>> Create(AnthropicManagedAgentEnvironmentCreateRequest request, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpPost<AnthropicManagedAgentEnvironment>(provider, Endpoint, postData: request.Serialize(), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Gets an environment by ID.
    /// </summary>
    public Task<HttpCallResult<AnthropicManagedAgentEnvironment>> Get(string environmentId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicManagedAgentEnvironment>(provider, Endpoint, provider.ApiUrl(Endpoint, GetResourcePath(environmentId)), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Lists environments.
    /// </summary>
    public Task<HttpCallResult<ListResponse<AnthropicManagedAgentEnvironment>>> List(ListQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<ListResponse<AnthropicManagedAgentEnvironment>>(
            provider,
            Endpoint,
            queryParams: AnthropicManagedAgentUrls.LegacyQuery(query),
            ct: cancellationToken,
            headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>Lists resources using native page/next_page cursors.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentPage<AnthropicManagedAgentEnvironment>>> ListPage(AnthropicManagedAgentPageQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicManagedAgentPage<AnthropicManagedAgentEnvironment>>(provider, Endpoint,
            AnthropicManagedAgentUrls.Query(provider.ApiUrl(Endpoint, null), query?.Parameters()),
            ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    private static string GetResourcePath(string environmentId) => AnthropicManagedAgentUrls.Resource(environmentId);
}
