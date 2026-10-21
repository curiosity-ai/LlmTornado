using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Responses.Events;
using LlmTornado.Threads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses;

/// <summary>Persistent Responses WebSocket connection. CreateResponseAsync follows steering successors;
/// SendResponseAsync and ReadEventsAsync provide explicit control over multiplexed lanes and tool continuations.
/// A connection permits one reader at a time and concurrent serialized sends.</summary>
public sealed class ResponsesWebSocketConnection : IAsyncDisposable
{
    private readonly ResponsesEndpoint endpoint;
    private readonly IEndpointProvider provider;
    private readonly ClientWebSocket webSocket;
    private readonly ResponsesWebSocketConnectOptions connectOptions;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private readonly object stateGate = new();
    private readonly Dictionary<string, ResponseInjectionState> injections = new();
    private readonly List<ResponseSteerSubmission> submissions = [];
    private readonly Dictionary<string, ResponseEventSteerPending> pendingSteers = new();
    private readonly Dictionary<string, ResponseResult> latestByLane = new();
    private readonly Dictionary<string, List<KeyValuePair<string?, ResponseRequest>>> queuedRequests = new();
    private readonly Dictionary<string, ResponseRequest> requestByResponse = new();
    private readonly Dictionary<string, string> activeByLane = new();
    private readonly HashSet<KeyValuePair<string, string>> requestedContinuations = [];
    private readonly byte[] receiveBuffer = new byte[8192];
    private int readerActive;
    private int closedNotified;
    private int disposed;

    internal ResponsesWebSocketConnection(ResponsesEndpoint endpoint, IEndpointProvider provider, ClientWebSocket webSocket, ResponsesWebSocketConnectOptions? options = null)
    {
        this.endpoint = endpoint;
        this.provider = provider;
        this.webSocket = webSocket;
        lifetimeToken = lifetime.Token;
        connectOptions = options ?? new();
    }

    public WebSocketState State => webSocket.State;
    /// <summary>The last terminal response across the connection, including incomplete and failed responses.</summary>
    public string? CurrentResponseId { get; private set; }
    public ResponseResult? CurrentResponse { get; private set; }
    /// <summary>The active response on the default lane, available before OnResponseCreated runs.</summary>
    public string? ActiveResponseId => GetActiveResponseId();
    public string? GetActiveResponseId(string? streamId = null) { lock (stateGate) return activeByLane.TryGetValue(streamId ?? "", out string? id) ? id : null; }
    public ResponseResult? GetCurrentResponse(string? streamId = null) { lock (stateGate) return latestByLane.TryGetValue(streamId ?? "", out ResponseResult? result) ? result : null; }

    /// <summary>Submission journal, including unacknowledged and unknown outcomes. No automatic retry occurs.</summary>
    public IReadOnlyList<ResponseSteerSubmission> SteeringSubmissions
    {
        get
        {
            lock (stateGate) return submissions.Select(x => new ResponseSteerSubmission
            {
                Id = x.Id, PreviousResponseId = x.PreviousResponseId, StreamId = x.StreamId, Input = x.Input?.DeepClone(), Status = x.Status,
                SuccessorResponseId = x.SuccessorResponseId, Error = x.Error
            }).ToList();
        }
    }
    /// <summary>Saved injection input and response-level acknowledgement counts.</summary>
    public IReadOnlyList<ResponseInjectionState> Injections
    {
        get
        {
            lock (stateGate) return injections.Values.Select(x => new ResponseInjectionState
            {
                ResponseId = x.ResponseId, PendingAcknowledgements = x.PendingAcknowledgements,
                AcceptedCount = x.AcceptedCount, UnknownAcknowledgements = x.UnknownAcknowledgements,
                SubmittedInputs = x.SubmittedInputs.Select(i => (JArray)i.DeepClone()).ToList(),
                Failures = x.Failures.Select(f => JObject.FromObject(f).ToObject<ResponseEventInjectFailed>()!).ToList()
            }).ToList();
        }
    }
    /// <summary>Queued steering that requires client input. Multiple entries may describe the same parent;
    /// send one continuation per parent, using saved tool results or explicit approval decisions.</summary>
    public IReadOnlyList<ResponseEventSteerPending> PendingSteers { get { lock (stateGate) return pendingSteers.Values.ToList(); } }

    /// <summary>Sends a response and reads through automatic steering successors. Returns at ordinary termination,
    /// or at a steer.pending event when the application must supply tool results or approval.
    /// Use SendResponseAsync inside callbacks; a second create-and-read operation would compete for the reader.</summary>
    public Task<ResponseResult?> CreateResponseAsync(ResponseRequest request, ResponseStreamEventHandler? eventsHandler = null,
        ResponseWebSocketCreateOptions? options = null, CancellationToken cancellationToken = default) =>
        CreateResponseInternalAsync(request, eventsHandler, options, false, cancellationToken);

    /// <summary>Like CreateResponseAsync, routing transport failures to OnException when provided. Cancellation still propagates.</summary>
    public Task<ResponseResult?> CreateResponseSafeAsync(ResponseRequest request, ResponseStreamEventHandler? eventsHandler = null,
        ResponseWebSocketCreateOptions? options = null, CancellationToken cancellationToken = default) =>
        CreateResponseInternalAsync(request, eventsHandler, options, true, cancellationToken);

    /// <summary>Sends a create event without starting another reader. Suitable for tool/approval results in callbacks,
    /// or for multiple stream_id lanes consumed by a single ReadEventsAsync enumeration.</summary>
    public async Task SendResponseAsync(ResponseRequest request, ResponseWebSocketCreateOptions? options = null, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        if ((request.RequiresMultiAgentBeta || request.ApiOptions?.HasMultiAgent == true) && !connectOptions.HasMultiAgent)
            throw new InvalidOperationException("Connect with EnableMultiAgent or the responses_multi_agent=v1 beta before sending multi-agent requests.");
        if (request.ApiOptions?.Headers() is { } requestHeaders &&
            requestHeaders["OpenAI-Beta"]?.ToString()?.Split(',').Any(x => !((string?)connectOptions.Headers()?["OpenAI-Beta"] ?? "").Split(',').Contains(x)) == true)
            throw new InvalidOperationException("Request beta headers must be configured when opening the WebSocket connection.");
        if (options?.ContinueFromCurrentResponse != false && request.PreviousResponseId is null)
            request.PreviousResponseId = GetCurrentResponse(options?.StreamId)?.Id;
        string payload = BuildCreatePayload(request, provider, options);
        // Register before sending, so a reader callback can observe an immediate response from the peer.
        string? parent = request.PreviousResponseId;
        if (parent is not null) { lock (stateGate) requestedContinuations.Add(new(options?.StreamId ?? "", parent)); }
        try
        {
            await SendTextAsync(payload, cancellationToken, () =>
            {
                lock (stateGate)
                {
                    string lane = options?.StreamId ?? "";
                    if (!queuedRequests.TryGetValue(lane, out var queue)) queuedRequests[lane] = queue = [];
                    queue.Add(new(parent, request));
                }
            }).ConfigureAwait(false);
        }
        catch
        {
            if (parent is not null) { lock (stateGate) requestedContinuations.Remove(new(options?.StreamId ?? "", parent)); }
            throw;
        }
    }

    /// <summary>Injects client function output into an active multi-agent response. Keep reading until the
    /// response terminates and all injections are acknowledged. Failed input is saved for explicit recovery.</summary>
    public async Task InjectAsync(ResponseInjectRequest request, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        if (!connectOptions.HasMultiAgent) throw new InvalidOperationException("Function injection requires the multi-agent beta on the WebSocket handshake.");
        JObject payload = request.ToPayload();
        await SendTextAsync(payload.ToString(Formatting.None), cancellationToken, () =>
        {
            lock (stateGate)
            {
                if (!injections.TryGetValue(request.ResponseId, out var state))
                    injections[request.ResponseId] = state = new() { ResponseId = request.ResponseId };
                state.PendingAcknowledgements++;
                state.SubmittedInputs = state.SubmittedInputs.Concat(new[] { (JArray)payload["input"]!.DeepClone() }).ToList();
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Queues steering on the target's lane. Keep reading to observe acceptance, failure and the successor's commit point.</summary>
    public async Task SteerAsync(ResponseSteerRequest request, CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        JObject payload = request.ToPayload();
        ResponseSteerSubmission submission = new()
        {
            PreviousResponseId = request.PreviousResponseId, Input = payload["input"]!.DeepClone(), Status = ResponseSteerSubmissionStatus.Sent
        };
        bool registered = false;
        try
        {
            await SendTextAsync(payload.ToString(Formatting.None), cancellationToken, () =>
            {
                lock (stateGate) submissions.Add(submission);
                registered = true;
            }).ConfigureAwait(false);
        }
        catch
        {
            // A failed/cancelled write is not proof that the server did not receive it.
            if (registered)
            {
                lock (stateGate)
                    if (submission.Status == ResponseSteerSubmissionStatus.Sent) submission.Status = ResponseSteerSubmissionStatus.Unknown;
            }
            if (webSocket.State != WebSocketState.Open) MarkDisconnected();
            throw;
        }
    }

    public Task SteerAsync(string previousResponseId, string input, CancellationToken cancellationToken = default) =>
        SteerAsync(new ResponseSteerRequest(previousResponseId, input), cancellationToken);

    /// <summary>Reads all lanes until peer closure or cancellation. Unknown events retain their raw payload.
    /// Do not enumerate alongside CreateResponseAsync or another reader.</summary>
    public async IAsyncEnumerable<ResponseWebSocketEvent> ReadEventsAsync(ResponseStreamEventHandler? eventsHandler = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AcquireReader();
        try
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
            while (webSocket.State == WebSocketState.Open)
            {
                ResponseWebSocketEvent? evt = await ReceiveEventAsync(linked.Token).ConfigureAwait(false);
                if (evt is null) yield break;
                await DispatchAsync(evt, eventsHandler, linked.Token).ConfigureAwait(false);
                yield return evt;
            }
            linked.Token.ThrowIfCancellationRequested();
        }
        finally { ReleaseReader(); }
    }

    private async Task<ResponseResult?> CreateResponseInternalAsync(ResponseRequest request, ResponseStreamEventHandler? handler,
        ResponseWebSocketCreateOptions? options, bool isSafe, CancellationToken cancellationToken)
    {
        try { AcquireReader(); }
        catch (InvalidOperationException ex) when (webSocket.State != WebSocketState.Open && isSafe && handler?.OnException is not null)
        {
            await handler.OnException(new TornadoStreamRequest { Exception = ex }).ConfigureAwait(false);
            return null;
        }
        try
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, request.CancellationToken, lifetimeToken);
            try { await SendResponseAsync(request, options, linked.Token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException && isSafe && handler?.OnException is not null)
            {
                await handler.OnException(new TornadoStreamRequest { Exception = ex }).ConfigureAwait(false);
                return null;
            }

            HashSet<string> responseIds = [];
            string lane = options?.StreamId ?? "";
            ResponseResult? terminal = null;
            bool expectingSuccessor = false;
            while (webSocket.State == WebSocketState.Open)
            {
                ResponseWebSocketEvent? evt;
                try { evt = await ReceiveEventAsync(linked.Token).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OperationCanceledException && isSafe && handler?.OnException is not null)
                {
                    await handler.OnException(new TornadoStreamRequest { Exception = ex }).ConfigureAwait(false);
                    return null;
                }
                if (evt is null)
                {
                    EndOfStreamException closed = new("Responses WebSocket closed before the response or steering continuation finished.");
                    if (isSafe && handler?.OnException is not null)
                    {
                        await handler.OnException(new TornadoStreamRequest { Exception = closed }).ConfigureAwait(false);
                        return null;
                    }
                    throw closed;
                }

                bool belongsToLane = (evt.StreamId ?? "") == lane || (evt.Event switch
                {
                    ResponseEventInjectCreated accepted => responseIds.Contains(accepted.ResponseId),
                    ResponseEventInjectFailed failedInjection => responseIds.Contains(failedInjection.ResponseId),
                    _ => false
                });
                if (!belongsToLane)
                {
                    if (handler?.OnWebSocketEvent is not null) await handler.OnWebSocketEvent(evt).ConfigureAwait(false);
                    continue;
                }

                if ((evt.StreamId ?? "") == lane && evt.Event is ResponseEventCreated && evt.Response is not null)
                {
                    responseIds.Add(evt.Response.Id);
                    terminal = null;
                    expectingSuccessor = false;
                }
                if ((evt.StreamId ?? "") == lane && evt.Event is ResponseEventCompleted or ResponseEventFailed or ResponseEventIncomplete)
                {
                    terminal = evt.Response;
                    if (terminal is not null) responseIds.Add(terminal.Id);
                    expectingSuccessor = terminal?.IncompleteDetails?.Reason == "steered";
                }

                await DispatchAsync(evt, handler, linked.Token).ConfigureAwait(false);
                if (!belongsToLane) continue;
                if (evt.Event is ResponseEventError) return null;
                if (evt.Event is ResponseEventSteerFailed failed && responseIds.Contains(failed.Steer.PreviousResponseId))
                    expectingSuccessor = false;
                if (terminal is null) continue;

                if (evt.Event is ResponseEventSteerPending pending && responseIds.Contains(pending.Steer.PreviousResponseId))
                {
                    bool continuing;
                    lock (stateGate) continuing = requestedContinuations.Contains(new(lane, pending.Steer.PreviousResponseId));
                    if (!continuing) return await FinishAsync(ProcessingRequest(terminal.Id, request), terminal).ConfigureAwait(false);
                }
                bool outstanding;
                bool explicitContinuation;
                lock (stateGate)
                {
                    outstanding = submissions.Any(x => responseIds.Contains(x.PreviousResponseId) && IsPending(x.Status)) ||
                        injections.Values.Any(x => responseIds.Contains(x.ResponseId) && x.PendingAcknowledgements > 0);
                    explicitContinuation = responseIds.Any(x => requestedContinuations.Contains(new(lane, x)));
                }
                if (!outstanding && !expectingSuccessor && !explicitContinuation)
                    return await FinishAsync(ProcessingRequest(terminal.Id, request), terminal).ConfigureAwait(false);
            }
            linked.Token.ThrowIfCancellationRequested();
            EndOfStreamException ended = new("Responses WebSocket closed before the response finished.");
            if (isSafe && handler?.OnException is not null)
            {
                await handler.OnException(new TornadoStreamRequest { Exception = ended }).ConfigureAwait(false);
                return null;
            }
            throw ended;
        }
        finally { ReleaseReader(); }
    }

    private async Task<ResponseResult> FinishAsync(ResponseRequest request, ResponseResult response)
    {
        if (response.Status == ResponseMessageStatuses.Completed)
        {
            HashSet<string> suppliedCalls;
            lock (stateGate) suppliedCalls = new HashSet<string>(injections.Values.SelectMany(x => x.SubmittedInputs)
                .SelectMany(x => x).Where(x => (string?)x["type"] == "function_call_output")
                .Select(x => (string?)x["call_id"]).OfType<string>());
            await ResponsesEndpoint.HandleResponse(request, response, suppliedCalls).ConfigureAwait(false);
        }
        return response;
    }

    private ResponseRequest ProcessingRequest(string responseId, ResponseRequest fallback)
    {
        lock (stateGate) return requestByResponse.TryGetValue(responseId, out ResponseRequest? request) ? request : fallback;
    }

    private async Task DispatchAsync(ResponseWebSocketEvent evt, ResponseStreamEventHandler? handler, CancellationToken cancellationToken)
    {
        if (handler?.OnWebSocketEvent is not null) await handler.OnWebSocketEvent(evt).ConfigureAwait(false);
        await endpoint.DispatchResponseStreamEventAsync(evt.Type, evt.Data, null, handler, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsPending(ResponseSteerSubmissionStatus status) => status is
        ResponseSteerSubmissionStatus.Sent or ResponseSteerSubmissionStatus.Accepted or ResponseSteerSubmissionStatus.WaitingForInput;

    private void UpdateState(ResponseWebSocketEvent evt)
    {
        lock (stateGate)
        {
            string lane = evt.StreamId ?? "";
            if (evt.Event is ResponseEventCreated && evt.Response is not null)
            {
                activeByLane[lane] = evt.Response.Id;
                string? parent = evt.Response.PreviousResponseId;
                if (queuedRequests.TryGetValue(lane, out var queue) && queue.Count > 0 && queue[0].Key == parent)
                {
                    requestByResponse[evt.Response.Id] = queue[0].Value;
                    queue.RemoveAt(0);
                }
                else if (parent is not null && requestByResponse.TryGetValue(parent, out ResponseRequest? inherited))
                    requestByResponse[evt.Response.Id] = inherited;
                if (parent is not null)
                {
                    requestedContinuations.Remove(new(lane, parent));
                    foreach (var submission in submissions.Where(x => x.PreviousResponseId == parent && (x.StreamId ?? "") == lane && x.Status is ResponseSteerSubmissionStatus.Accepted or ResponseSteerSubmissionStatus.WaitingForInput))
                    {
                        submission.Status = ResponseSteerSubmissionStatus.Committed;
                        submission.SuccessorResponseId = evt.Response.Id;
                        if (submission.Id is not null) pendingSteers.Remove(submission.Id);
                    }
                }
            }
            if (evt.Event is ResponseEventCompleted or ResponseEventFailed or ResponseEventIncomplete && evt.Response is not null)
            {
                CurrentResponse = evt.Response;
                CurrentResponseId = evt.Response.Id;
                latestByLane[lane] = evt.Response;
                if (activeByLane.TryGetValue(lane, out string? active) && active == evt.Response.Id) activeByLane.Remove(lane);
            }

            string? injectedResponseId = evt.Event switch
            {
                ResponseEventInjectCreated created => created.ResponseId,
                ResponseEventInjectFailed failed => failed.ResponseId,
                _ => null
            };
            if (injectedResponseId is not null)
            {
                if (!injections.TryGetValue(injectedResponseId, out var state))
                    injections[injectedResponseId] = state = new() { ResponseId = injectedResponseId };
                if (state.PendingAcknowledgements > 0) state.PendingAcknowledgements--;
                if (evt.Event is ResponseEventInjectCreated) state.AcceptedCount++;
                if (evt.Event is ResponseEventInjectFailed failed) state.Failures = state.Failures.Concat(new[] { failed }).ToList();
            }

            ResponseSteer? steer = evt.Event switch
            {
                ResponseEventSteerAccepted accepted => accepted.Steer,
                ResponseEventSteerPending pending => pending.Steer,
                ResponseEventSteerFailed failed => failed.Steer,
                _ => null
            };
            if (steer is null) return;
            var entry = steer.Id is not null ? submissions.FirstOrDefault(x => x.Id == steer.Id) : null;
            entry ??= submissions.FirstOrDefault(x => x.PreviousResponseId == steer.PreviousResponseId && x.Id is null && x.Status is ResponseSteerSubmissionStatus.Sent or ResponseSteerSubmissionStatus.Unknown);
            if (entry is null)
            {
                entry = new() { PreviousResponseId = steer.PreviousResponseId };
                submissions.Add(entry);
            }
            entry.Id = steer.Id ?? entry.Id;
            entry.StreamId = evt.StreamId;
            entry.Input ??= steer.Input?.DeepClone();
            switch (evt.Event)
            {
                case ResponseEventSteerAccepted:
                    entry.Status = ResponseSteerSubmissionStatus.Accepted;
                    break;
                case ResponseEventSteerPending pending:
                    entry.Status = ResponseSteerSubmissionStatus.WaitingForInput;
                    if (entry.Id is not null) pendingSteers[entry.Id] = pending;
                    break;
                case ResponseEventSteerFailed failed:
                    entry.Status = ResponseSteerSubmissionStatus.Failed;
                    entry.Error = failed.Error;
                    if (entry.Id is not null) pendingSteers.Remove(entry.Id);
                    break;
            }
        }
    }

    private void AcquireReader()
    {
        EnsureOpen();
        if (Interlocked.CompareExchange(ref readerActive, 1, 0) != 0)
            throw new InvalidOperationException("This connection already has a reader. Use SendResponseAsync from callbacks, or one ReadEventsAsync reader for multiple lanes.");
    }
    private void ReleaseReader()
    {
        if (webSocket.State != WebSocketState.Open) MarkDisconnected();
        Interlocked.Exchange(ref readerActive, 0);
    }
    private void EnsureOpen()
    {
        if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(ResponsesWebSocketConnection));
        if (webSocket.State != WebSocketState.Open) throw new InvalidOperationException("WebSocket is not open.");
    }
    private void MarkDisconnected()
    {
        lock (stateGate)
        {
            foreach (var submission in submissions.Where(x => IsPending(x.Status))) submission.Status = ResponseSteerSubmissionStatus.Unknown;
            foreach (var state in injections.Values)
            {
                state.UnknownAcknowledgements += state.PendingAcknowledgements;
                state.PendingAcknowledgements = 0;
            }
            activeByLane.Clear();
        }
        if (Interlocked.Exchange(ref closedNotified, 1) == 0) connectOptions.OnClose?.Invoke();
    }

    /// <summary>Sends a close frame without competing with the active receive operation for another read.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
        }
        finally { sendLock.Release(); }
        MarkDisconnected();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel();
        webSocket.Abort();
        await sendLock.WaitAsync().ConfigureAwait(false);
        webSocket.Dispose();
        sendLock.Dispose();
        lifetime.Dispose();
        MarkDisconnected();
    }

    internal static string BuildWebSocketUrl(IEndpointProvider provider, string? urlOverride)
    {
        if (!string.IsNullOrWhiteSpace(urlOverride))
        {
            return urlOverride;
        }

        string httpUrl = provider.ApiUrl(CapabilityEndpoints.Responses, null);
        string wsUrl = httpUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? "wss://" + httpUrl.Substring(8)
            : httpUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? "ws://" + httpUrl.Substring(7)
                : httpUrl;
        return wsUrl;
    }

    internal static void ApplyAuthHeaders(ClientWebSocket webSocket, IEndpointProvider provider, ResponseApiOptions? options = null)
    {
        ProviderAuthentication? auth = provider.Api?.GetProvider(provider.Provider).Auth;

        if (auth?.ApiKey is not null)
        {
            webSocket.Options.SetRequestHeader("Authorization", $"Bearer {auth.ApiKey.Trim()}");
        }

        if (auth?.Organization is not null)
        {
            webSocket.Options.SetRequestHeader("OpenAI-Organization", auth.Organization.Trim());
        }

        if (options?.Headers() is { } headers)
            foreach (var header in headers) webSocket.Options.SetRequestHeader(header.Key, header.Value?.ToString());
        webSocket.Options.SetRequestHeader("User-Agent", EndpointBase.ResolveUserAgent(provider.Api));
    }

    internal static string BuildCreatePayload(ResponseRequest request, IEndpointProvider provider, ResponseWebSocketCreateOptions? options)
    {
        TornadoRequestContent serialized = request.Serialize(provider);
        string bodyJson = serialized.Body as string ?? JsonConvert.SerializeObject(serialized.Body, EndpointBase.NullSettings);
        JObject jo = JObject.Parse(bodyJson);

        jo.Remove("stream");
        jo.Remove("background");
        jo.Remove("stream_options");
        jo["type"] = "response.create";

        if (options?.Generate is not null)
        {
            jo["generate"] = options.Generate.Value;
        }

        if (options?.StreamId is not null) jo["stream_id"] = options.StreamId;
        return jo.ToString(Formatting.None);
    }


    private async Task SendTextAsync(string text, CancellationToken cancellationToken, Action? beforeSend = null)
    {
        EnsureOpen();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await sendLock.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            beforeSend?.Invoke();
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, linked.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // A failed write can leave a partially transmitted frame; do not reuse uncertain request ordering.
                webSocket.Abort();
                MarkDisconnected();
                if (ex is not OperationCanceledException) connectOptions.OnError?.Invoke(ex);
                throw;
            }
        }
        finally { sendLock.Release(); }
    }

    private async Task<ResponseWebSocketEvent?> ReceiveEventAsync(CancellationToken cancellationToken)
    {
        try
        {
            string? data = await ReceiveTextMessageAsync(cancellationToken).ConfigureAwait(false);
            if (data is null) { MarkDisconnected(); return null; }
            ResponseWebSocketEvent evt = ResponseWebSocketEvent.Parse(data);
            UpdateState(evt);
            return evt;
        }
        catch (Exception ex)
        {
            if (webSocket.State != WebSocketState.Open) MarkDisconnected();
            if (ex is not OperationCanceledException) connectOptions.OnError?.Invoke(ex);
            throw;
        }
    }

    private async Task<string?> ReceiveTextMessageAsync(CancellationToken cancellationToken)
    {
        using MemoryStream message = new();
        while (webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text) throw new InvalidDataException("Responses WebSocket returned a non-text message.");
            message.Write(receiveBuffer, 0, result.Count);
            if (result.EndOfMessage) return Encoding.UTF8.GetString(message.ToArray());
        }
        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }
}
