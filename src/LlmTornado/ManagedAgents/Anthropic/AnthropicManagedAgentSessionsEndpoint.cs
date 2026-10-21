using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Code.Sse;
using LlmTornado.Common;
using LlmTornado.Threads;

namespace LlmTornado.ManagedAgents.Anthropic;

/// <summary>
/// Claude Managed Agent sessions — create, run, send events, and stream SSE responses.
/// </summary>
public class AnthropicManagedAgentSessionsEndpoint : EndpointBase
{
    internal AnthropicManagedAgentSessionsEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.AnthropicManagedAgentSessions;

    /// <summary>
    /// Creates a session (<c>POST /v1/sessions</c>).
    /// </summary>
    public Task<HttpCallResult<AnthropicManagedAgentSession>> Create(AnthropicManagedAgentSessionCreateRequest request, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpPost<AnthropicManagedAgentSession>(provider, Endpoint, postData: request.Serialize(), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    public Task<HttpCallResult<AnthropicManagedAgentSession>> Get(string sessionId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicManagedAgentSession>(provider, Endpoint, provider.ApiUrl(Endpoint, GetResourcePath(sessionId)), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Lists sessions.
    /// </summary>
    public Task<HttpCallResult<ListResponse<AnthropicManagedAgentSession>>> List(ListQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<ListResponse<AnthropicManagedAgentSession>>(
            provider,
            Endpoint,
            queryParams: AnthropicManagedAgentUrls.LegacyQuery(query, order: true),
            ct: cancellationToken,
            headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Updates session metadata, title, or session-local agent tool overrides.
    /// </summary>
    public Task<HttpCallResult<AnthropicManagedAgentSession>> Update(string sessionId, AnthropicManagedAgentSessionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpPost<AnthropicManagedAgentSession>(provider, Endpoint, provider.ApiUrl(Endpoint, GetResourcePath(sessionId)), postData: request.Serialize(), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Deletes a session and its server-side state.
    /// </summary>
    public async Task<HttpCallResult<bool>> Delete(string sessionId, CancellationToken cancellationToken = default)
    {
        HttpCallResult<AnthropicManagedAgentDeletedSession> result = await DeleteResource(sessionId, cancellationToken).ConfigureAwait(false);
        return new HttpCallResult<bool>(result.Code, result.Response, result.Ok && result.Data?.Type == "session_deleted", result.Ok, result.Request)
        {
            Exception = result.Exception
        };
    }

    /// <summary>Deletes a session and returns the native confirmation with its ID.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentDeletedSession>> DeleteResource(string sessionId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpDeleteRaw<AnthropicManagedAgentDeletedSession>(provider, Endpoint, provider.ApiUrl(Endpoint, GetResourcePath(sessionId)), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Archives a session (read-only, not reversible).
    /// </summary>
    public Task<HttpCallResult<AnthropicManagedAgentSession>> Archive(string sessionId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpPost<AnthropicManagedAgentSession>(provider, Endpoint, provider.ApiUrl(Endpoint, $"{GetResourcePath(sessionId)}/archive"), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Sends events to a session (user messages, tool results, interrupts).
    /// </summary>
    public Task<HttpCallResult<object>> SendEvents(string sessionId, AnthropicManagedAgentSendEventsRequest request, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpPost<object>(provider, Endpoint, provider.ApiUrl(Endpoint, $"{GetResourcePath(sessionId)}/events"), postData: request.Serialize(), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>Sends events and returns their persisted IDs and processing metadata.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentSentEvents>> SendEventsWithResult(string sessionId, AnthropicManagedAgentSendEventsRequest request, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpPost<AnthropicManagedAgentSentEvents>(provider, Endpoint, provider.ApiUrl(Endpoint, $"{GetResourcePath(sessionId)}/events"),
            postData: request.Serialize(), ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Lists session events (paginated polling).
    /// </summary>
    public Task<HttpCallResult<ListResponse<AnthropicManagedAgentEvent>>> ListEvents(string sessionId, ListQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<ListResponse<AnthropicManagedAgentEvent>>(
            provider,
            Endpoint,
            provider.ApiUrl(Endpoint, $"{GetResourcePath(sessionId)}/events"),
            queryParams: AnthropicManagedAgentUrls.LegacyQuery(query, order: true),
            ct: cancellationToken,
            headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>
    /// Streams session events via SSE (<c>GET /v1/sessions/{id}/events/stream</c>).
    /// Start enumerating before sending user events to avoid missing early lifecycle events.
    /// </summary>
    public IAsyncEnumerable<AnthropicManagedAgentStreamEvent> StreamEvents(
        string sessionId,
        AnthropicManagedAgentStreamEventHandler? handler = null,
        CancellationToken cancellationToken = default) =>
        StreamResource($"{GetResourcePath(sessionId)}/events/stream", null, handler, cancellationToken);

    /// <summary>Streams session events with optional best-effort previews. Idle does not end an open workflow.</summary>
    public IAsyncEnumerable<AnthropicManagedAgentStreamEvent> StreamEventsWithOptions(
        string sessionId,
        AnthropicManagedAgentStreamOptions options,
        AnthropicManagedAgentStreamEventHandler? handler = null,
        CancellationToken cancellationToken = default) =>
        StreamResource($"{GetResourcePath(sessionId)}/events/stream", options, handler, cancellationToken);

    private async IAsyncEnumerable<AnthropicManagedAgentStreamEvent> StreamResource(
        string path,
        AnthropicManagedAgentStreamOptions? options,
        AnthropicManagedAgentStreamEventHandler? handler,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        await using TornadoStreamRequest streamRequest = await HttpStreamingRequestData(
            provider, Endpoint,
            url: AnthropicManagedAgentUrls.Query(provider.ApiUrl(Endpoint, path), options?.Parameters()),
            verb: HttpVerbs.Get,
            headers: VendorAnthropicManagedAgentsConstants.ApiHeaders,
            token: cancellationToken).ConfigureAwait(false);

        if (streamRequest.Exception is not null)
        {
            throw streamRequest.Exception;
        }

        await foreach (SseItem<string> item in SseParser.Create(streamRequest.StreamReader.BaseStream).EnumerateAsync(cancellationToken).ConfigureAwait(false))
        {
            ServerSentEvent sse = new() { Data = item.Data, EventType = item.EventType };
            if (handler?.OnSse is not null)
            {
                await handler.OnSse(sse).ConfigureAwait(false);
            }

            AnthropicManagedAgentStreamEvent evt = AnthropicManagedAgentStreamEvent.FromServerSentEvent(sse);

            if (handler?.OnPreview is not null && evt.Event?.Type is "event_start" or "event_delta")
                await handler.OnPreview(evt.Event).ConfigureAwait(false);

            if (handler?.OnTextDelta is not null && evt.Event?.Content is not null)
            {
                foreach (Newtonsoft.Json.Linq.JToken block in evt.Event.Content)
                {
                    string? text = block["text"]?.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        await handler.OnTextDelta(text).ConfigureAwait(false);
                    }
                }
            }

            if (handler?.OnSessionIdle is not null && evt.Event?.Type is "session.status_idle")
            {
                await handler.OnSessionIdle(evt.Event).ConfigureAwait(false);
            }

            if (handler?.OnWorkflowRun is not null && evt.Event?.Type?.StartsWith("workflow_run.", System.StringComparison.Ordinal) == true)
                await handler.OnWorkflowRun(evt.Event).ConfigureAwait(false);

            if (handler?.OnToolUse is not null && evt.Event?.Type is "agent.tool_use" or "agent.mcp_tool_use" or "agent.custom_tool_use")
                await handler.OnToolUse(evt.Event).ConfigureAwait(false);

            if (handler?.OnEvent is not null)
            {
                await handler.OnEvent(evt).ConfigureAwait(false);
            }

            yield return evt;
        }
    }

    /// <summary>Lists sessions using native page/next_page cursors.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentPage<AnthropicManagedAgentSession>>> ListPage(AnthropicManagedAgentPageQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicManagedAgentPage<AnthropicManagedAgentSession>>(provider, Endpoint,
            AnthropicManagedAgentUrls.Query(provider.ApiUrl(Endpoint, null), query?.Parameters()),
            ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>Lists authoritative session events with native cursors and repeated type filters.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentPage<AnthropicManagedAgentEvent>>> ListEventsPage(string sessionId, AnthropicManagedAgentEventQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicManagedAgentPage<AnthropicManagedAgentEvent>>(provider, Endpoint,
            AnthropicManagedAgentUrls.Query(provider.ApiUrl(Endpoint, $"{GetResourcePath(sessionId)}/events"), query?.Parameters()),
            ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>Lists primary, child, workflow and advisor threads.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentPage<AnthropicManagedAgentThread>>> ListThreads(string sessionId, AnthropicManagedAgentThreadQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicManagedAgentPage<AnthropicManagedAgentThread>>(provider, Endpoint,
            AnthropicManagedAgentUrls.Query(provider.ApiUrl(Endpoint, $"{GetResourcePath(sessionId)}/threads"), query?.Parameters()),
            ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>Retrieves a session thread.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentThread>> GetThread(string sessionId, string threadId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicManagedAgentThread>(provider, Endpoint, provider.ApiUrl(Endpoint, GetThreadPath(sessionId, threadId)),
            ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>Archives a thread permanently.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentThread>> ArchiveThread(string sessionId, string threadId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpPost<AnthropicManagedAgentThread>(provider, Endpoint, provider.ApiUrl(Endpoint, GetThreadPath(sessionId, threadId) + "/archive"),
            ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>Lists authoritative events for a single thread.</summary>
    public Task<HttpCallResult<AnthropicManagedAgentPage<AnthropicManagedAgentEvent>>> ListThreadEvents(string sessionId, string threadId, AnthropicManagedAgentPageQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicManagedAgentPage<AnthropicManagedAgentEvent>>(provider, Endpoint,
            AnthropicManagedAgentUrls.Query(provider.ApiUrl(Endpoint, GetThreadPath(sessionId, threadId) + "/events"), query?.Parameters()),
            ct: cancellationToken, headers: VendorAnthropicManagedAgentsConstants.ApiHeaders);
    }

    /// <summary>Streams a single thread, without replay. Open before listing history and deduplicate event IDs.</summary>
    public IAsyncEnumerable<AnthropicManagedAgentStreamEvent> StreamThreadEvents(string sessionId, string threadId,
        AnthropicManagedAgentStreamEventHandler? handler = null, CancellationToken cancellationToken = default) =>
        StreamResource(GetThreadPath(sessionId, threadId) + "/stream", null, handler, cancellationToken);

    /// <summary>Streams a thread with optional preview deltas. Previews are scoped to this connection's thread.</summary>
    public IAsyncEnumerable<AnthropicManagedAgentStreamEvent> StreamThreadEventsWithOptions(string sessionId, string threadId,
        AnthropicManagedAgentStreamOptions options, AnthropicManagedAgentStreamEventHandler? handler = null, CancellationToken cancellationToken = default) =>
        StreamResource(GetThreadPath(sessionId, threadId) + "/stream", options, handler, cancellationToken);

    private static string GetThreadPath(string sessionId, string threadId) => GetResourcePath(sessionId) + "/threads" + AnthropicManagedAgentUrls.Resource(threadId);
    private static string GetResourcePath(string sessionId) => AnthropicManagedAgentUrls.Resource(sessionId);
}
