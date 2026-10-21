using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Decision.OpenAi;

/// <summary>Native OpenAI Decisions API, available through TornadoApi.Decision.OpenAi.</summary>
public class OpenAiDecisionEndpoint : EndpointBase
{
    internal OpenAiDecisionEndpoint(TornadoApi api) : base(api) { }

    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Decision;

    /// <summary>Evaluates text/images against an ordered list of native questions.</summary>
    public Task<HttpCallResult<OpenAiDecisionResult>> CreateSafeAsync(OpenAiDecisionRequest request, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<OpenAiDecisionResult>(provider, Endpoint, url: null, postData: request.Serialize(), ct: cancellationToken);
    }

    /// <summary>Evaluates native questions, throwing request or transport errors.</summary>
    public async Task<OpenAiDecisionResult?> CreateAsync(OpenAiDecisionRequest request, CancellationToken cancellationToken = default)
    {
        HttpCallResult<OpenAiDecisionResult> result = await CreateSafeAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Exception is not null) throw result.Exception;
        return result.Data;
    }
}
