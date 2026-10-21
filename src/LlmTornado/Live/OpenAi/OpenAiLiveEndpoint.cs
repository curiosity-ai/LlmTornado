using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;

namespace LlmTornado.Live.OpenAi;

/// <summary>
/// OpenAI GPT-Live API: WebSocket sessions, WebRTC create/fork, recording download, and SIP call control.
/// </summary>
public class OpenAiLiveEndpoint : EndpointBase
{
    internal OpenAiLiveEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Live;

    /// <summary>
    /// Opens a primary GPT-Live WebSocket and sends <c>session.start</c> unless disabled.
    /// </summary>
    public Task<OpenAiLiveSession> ConnectAsync(OpenAiLiveConnectOptions? options = null, CancellationToken cancellationToken = default)
    {
        OpenAiLiveConnectOptions connect = options ?? OpenAiLiveConnectOptions.Primary(OpenAiLiveSessionConfig.ForWebSocket());
        if (cancellationToken != default && connect.CancellationToken == default)
        {
            connect.CancellationToken = cancellationToken;
        }

        return OpenAiLiveSession.ConnectAsync(Api, connect);
    }

    /// <summary>
    /// Opens a primary WebSocket with the given session configuration.
    /// </summary>
    public Task<OpenAiLiveSession> ConnectAsync(OpenAiLiveSessionConfig session, CancellationToken cancellationToken = default)
    {
        return ConnectAsync(OpenAiLiveConnectOptions.Primary(session), cancellationToken);
    }

    /// <summary>
    /// Attaches a sideband WebSocket to an existing WebRTC or SIP session. Does not send <c>session.start</c>.
    /// </summary>
    public Task<OpenAiLiveSession> AttachAsync(string sessionId, OpenAiLiveConnectOptions? options = null, CancellationToken cancellationToken = default)
    {
        OpenAiLiveConnectOptions connect = options ?? OpenAiLiveConnectOptions.Sideband(sessionId);
        connect.Kind = OpenAiLiveConnectKind.Sideband;
        connect.SessionId = sessionId;
        connect.AutoStart = false;
        if (cancellationToken != default && connect.CancellationToken == default)
        {
            connect.CancellationToken = cancellationToken;
        }

        return OpenAiLiveSession.ConnectAsync(Api, connect);
    }

    /// <summary>
    /// Opens a WebSocket fork of a stored session and sends <c>session.start</c>.
    /// </summary>
    public Task<OpenAiLiveSession> ForkAsync(string sourceSessionId, OpenAiLiveSessionConfig? overrides = null, CancellationToken cancellationToken = default)
    {
        return ConnectAsync(OpenAiLiveConnectOptions.Fork(sourceSessionId, overrides), cancellationToken);
    }

    /// <summary>
    /// Creates a WebRTC session: <c>POST /v1/live/sessions</c>.
    /// Apply <c>transport.sdp</c> as the peer connection answer. Do not send <c>session.start</c> on the data channel.
    /// </summary>
    public Task<HttpCallResult<OpenAiLiveCreateResponse>> CreateWebRtcAsync(
        OpenAiLiveCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<OpenAiLiveCreateResponse>(provider, Endpoint, url: null, postData: Serialize(request), ct: cancellationToken);
    }

    /// <summary>
    /// Creates a WebRTC session from a session config and SDP offer.
    /// </summary>
    public Task<HttpCallResult<OpenAiLiveCreateResponse>> CreateWebRtcAsync(
        OpenAiLiveSessionConfig session,
        string sdpOffer,
        OpenAiLivePermissions? permissions = null,
        CancellationToken cancellationToken = default)
    {
        return CreateWebRtcAsync(OpenAiLiveCreateRequest.WebRtc(session, sdpOffer, permissions), cancellationToken);
    }

    /// <summary>
    /// Forks a stored session over WebRTC: <c>POST /v1/live/sessions/{id}/fork</c>.
    /// </summary>
    public Task<HttpCallResult<OpenAiLiveCreateResponse>> ForkWebRtcAsync(
        string sourceSessionId,
        OpenAiLiveCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<OpenAiLiveCreateResponse>(provider, Endpoint, ResolveRestUrl($"/{sourceSessionId}/fork"), postData: Serialize(request), ct: cancellationToken);
    }

    /// <summary>
    /// Forks a stored session over WebRTC from an SDP offer.
    /// </summary>
    public Task<HttpCallResult<OpenAiLiveCreateResponse>> ForkWebRtcAsync(
        string sourceSessionId,
        string sdpOffer,
        OpenAiLiveSessionConfig? session = null,
        OpenAiLivePermissions? permissions = null,
        CancellationToken cancellationToken = default)
    {
        return ForkWebRtcAsync(sourceSessionId, new OpenAiLiveCreateRequest
        {
            Session = session,
            Transport = OpenAiLiveTransport.WebRtc(sdpOffer),
            Permissions = permissions
        }, cancellationToken);
    }

    /// <summary>
    /// Downloads the finalized stereo WAV recording: <c>GET /v1/live/sessions/{id}/content</c>.
    /// Left channel is input, right channel is output.
    /// </summary>
    public async Task<StreamResponse?> DownloadRecordingAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return await HttpGetStream(provider, Endpoint, ResolveRestUrl($"/{sessionId}/content"), ct: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads the finalized recording as bytes.
    /// </summary>
    public async Task<byte[]?> DownloadRecordingBytesAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        StreamResponse? response = await DownloadRecordingAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (response?.Stream is null)
        {
            return null;
        }

        try
        {
            using MemoryStream buffer = new MemoryStream();
#if MODERN
            await response.Stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
#else
            await response.Stream.CopyToAsync(buffer).ConfigureAwait(false);
#endif
            return buffer.ToArray();
        }
        finally
        {
            response.Stream.Dispose();
            response.Response.Dispose();
        }
    }

    /// <summary>
    /// Accepts an inbound SIP call: <c>POST /v1/live/sessions/{id}/accept</c>.
    /// </summary>
    public Task<HttpCallResult<OpenAiLiveEmptyResponse>> AcceptAsync(
        string sessionId,
        OpenAiLiveSessionConfig session,
        CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<OpenAiLiveEmptyResponse>(
            provider,
            Endpoint,
            ResolveRestUrl($"/{sessionId}/accept"),
            postData: Serialize(OpenAiLiveAcceptRequest.Create(session)),
            ct: cancellationToken);
    }

    /// <summary>
    /// Rejects an inbound SIP call: <c>POST /v1/live/sessions/{id}/reject</c>.
    /// </summary>
    public Task<HttpCallResult<OpenAiLiveEmptyResponse>> RejectAsync(
        string sessionId,
        int? statusCode = 603,
        CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<OpenAiLiveEmptyResponse>(
            provider,
            Endpoint,
            ResolveRestUrl($"/{sessionId}/reject"),
            postData: Serialize(new OpenAiLiveRejectRequest { StatusCode = statusCode }),
            ct: cancellationToken);
    }

    /// <summary>
    /// Transfers an active SIP call: <c>POST /v1/live/sessions/{id}/refer</c>.
    /// </summary>
    public Task<HttpCallResult<OpenAiLiveEmptyResponse>> ReferAsync(
        string sessionId,
        string targetUri,
        CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<OpenAiLiveEmptyResponse>(
            provider,
            Endpoint,
            ResolveRestUrl($"/{sessionId}/refer"),
            postData: Serialize(OpenAiLiveReferRequest.To(targetUri)),
            ct: cancellationToken);
    }

    /// <summary>
    /// Hangs up a SIP or WebRTC session: <c>POST /v1/live/sessions/{id}/hangup</c>.
    /// </summary>
    public Task<HttpCallResult<OpenAiLiveEmptyResponse>> HangupAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<OpenAiLiveEmptyResponse>(provider, Endpoint, ResolveRestUrl($"/{sessionId}/hangup"), postData: "{}", ct: cancellationToken);
    }

    internal string ResolveRestUrl(string? suffix = null)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return GetUrl(provider, suffix);
    }

    private static string Serialize(object? data)
    {
        return JsonConvert.SerializeObject(data, OpenAiLiveJson.Settings);
    }
}
