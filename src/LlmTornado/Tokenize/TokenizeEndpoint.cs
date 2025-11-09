using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Tokenize;

/// <summary>
///     Tokenize API endpoint. Use this endpoint to count tokens in text or messages.
/// </summary>
public class TokenizeEndpoint : EndpointBase
{
    /// <summary>
    ///     Constructor of the api endpoint.  Rather than instantiating this yourself, access it through an instance of
    ///     <see cref="TornadoApi" /> as <see cref="TornadoApi.Tokenize" />.
    /// </summary>
    /// <param name="api"></param>
    internal TokenizeEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <summary>
    ///     The name of the endpoint, which is the final path segment in the API URL.  For example, "tokenize".
    /// </summary>
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Tokenize;

    /// <summary>
    ///     Counts tokens in the provided request.
    /// </summary>
    /// <param name="request">Request to be sent</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>
    ///     Asynchronously returns the tokenize result with token counts
    /// </returns>
    public async Task<TokenizeResult?> CountTokens(TokenizeRequest request, CancellationToken ct = default)
    {
        HttpCallResult<TokenizeResult> result = await CountTokensSafe(request, ct).ConfigureAwait(false);
        return result.Exception is not null ? throw result.Exception : result.Data;
    }
    
    /// <summary>
    ///     Counts tokens in the provided request.
    /// </summary>
    /// <param name="request">Request to be sent</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>
    ///     Asynchronously returns the tokenize result with token counts
    /// </returns>
    public async Task<HttpCallResult<TokenizeResult>> CountTokensSafe(TokenizeRequest request, CancellationToken ct = default)
    {
        IEndpointProvider provider = Api.GetProvider(request.Model ?? ChatModel.OpenAi.Gpt35.Turbo);
        TornadoRequestContent requestBody = request.Serialize(provider);
        return await HttpPost<TokenizeResult>(provider, Endpoint, requestBody.Url, requestBody.Body, request.Model, request, ct).ConfigureAwait(false);
    }
}

