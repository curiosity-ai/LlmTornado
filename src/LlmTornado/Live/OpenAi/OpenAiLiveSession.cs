using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Live.OpenAi;

/// <summary>
/// Active GPT-Live WebSocket session (primary, sideband, or fork).
/// Uses <see cref="System.Net.WebSockets.ClientWebSocket"/> and Newtonsoft.Json only.
/// </summary>
public sealed class OpenAiLiveSession : IAsyncDisposable
{
    private readonly ClientWebSocket webSocket;
    private readonly OpenAiLiveConnectOptions options;
    private readonly CancellationTokenSource linkedCts;
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private readonly TaskCompletionSource<OpenAiLiveSessionStartedEvent> started = new TaskCompletionSource<OpenAiLiveSessionStartedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<OpenAiLiveSessionClosedEvent> closed = new TaskCompletionSource<OpenAiLiveSessionClosedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? receiveTask;

    private OpenAiLiveSession(ClientWebSocket webSocket, OpenAiLiveConnectOptions options, CancellationTokenSource linkedCts)
    {
        this.webSocket = webSocket;
        this.options = options;
        this.linkedCts = linkedCts;
    }

    /// <summary>
    /// Connection kind used to open this socket.
    /// </summary>
    public OpenAiLiveConnectKind Kind => options.Kind;

    /// <summary>
    /// Whether the socket is open.
    /// </summary>
    public bool IsOpen => webSocket.State == WebSocketState.Open;

    /// <summary>
    /// Session id from <c>session.started</c>, or the attach/fork id supplied at connect.
    /// </summary>
    public string? SessionId { get; private set; }

    /// <summary>
    /// Latest resolved session snapshot.
    /// </summary>
    public OpenAiLiveSessionResource? Session { get; private set; }

    /// <summary>
    /// Latest cumulative usage snapshot.
    /// </summary>
    public OpenAiLiveUsage? Usage { get; private set; }

    /// <summary>
    /// Final close event when received.
    /// </summary>
    public OpenAiLiveSessionClosedEvent? Closed { get; private set; }

    /// <summary>
    /// Whether <c>session.started</c> has been received.
    /// </summary>
    public bool IsStarted => started.Task.Status == TaskStatus.RanToCompletion;

    /// <summary>
    /// Whether <c>session.closed</c> has been received.
    /// </summary>
    public bool IsFinalized => closed.Task.Status == TaskStatus.RanToCompletion;

    internal static async Task<OpenAiLiveSession> ConnectAsync(TornadoApi api, OpenAiLiveConnectOptions options)
    {
        string? apiKey = options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ProviderAuthentication? auth = api.GetProviderAuthentication(LLmProviders.OpenAi);
            apiKey = auth?.ApiKey;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key or ephemeral client secret is required for GPT-Live WebSocket connections.");
        }

        if (options.Kind is OpenAiLiveConnectKind.Sideband or OpenAiLiveConnectKind.Fork && string.IsNullOrWhiteSpace(options.SessionId))
        {
            throw new InvalidOperationException($"{options.Kind} connections require SessionId.");
        }

        ClientWebSocket ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", $"Bearer {apiKey.Trim()}");

        if (!string.IsNullOrWhiteSpace(options.SafetyIdentifier))
        {
            ws.Options.SetRequestHeader("OpenAI-Safety-Identifier", options.SafetyIdentifier);
        }

        if (!string.IsNullOrWhiteSpace(options.Organization))
        {
            ws.Options.SetRequestHeader("OpenAI-Organization", options.Organization);
        }

        if (!string.IsNullOrWhiteSpace(options.Project))
        {
            ws.Options.SetRequestHeader("OpenAI-Project", options.Project);
        }

        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        await ws.ConnectAsync(options.BuildWebSocketUri(), linked.Token).ConfigureAwait(false);

        OpenAiLiveSession session = new OpenAiLiveSession(ws, options, linked)
        {
            SessionId = options.SessionId
        };

        if (options.Kind == OpenAiLiveConnectKind.Sideband)
        {
            session.MarkSidebandReady();
        }

        options.OnOpen?.Invoke();
        session.receiveTask = session.ReceiveLoopAsync();

        if (options.ShouldSendSessionStart)
        {
            await session.StartAsync(options.Session ?? new OpenAiLiveSessionConfig(), options.StartEventId, linked.Token).ConfigureAwait(false);
        }

        return session;
    }

    /// <summary>
    /// Sends <c>session.start</c>. Required on primary and fork sockets. Do not send on a sideband.
    /// </summary>
    public Task StartAsync(OpenAiLiveSessionConfig session, string? eventId = null, CancellationToken cancellationToken = default)
    {
        if (options.Kind == OpenAiLiveConnectKind.Sideband)
        {
            throw new InvalidOperationException("Do not send session.start on a sideband connection. The session is already running.");
        }

        return SendAsync(OpenAiLiveClientEvents.SessionStart(session, eventId), cancellationToken);
    }

    /// <summary>
    /// Sends a sparse <c>session.update</c> replacing <c>delegation.responses</c>.
    /// </summary>
    public Task UpdateResponsesAsync(OpenAiLiveResponsesDelegation responses, string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.SessionUpdate(responses, eventId), cancellationToken);
    }

    /// <summary>
    /// Appends base64-encoded raw audio. PCM16 chunks must have an even byte length.
    /// </summary>
    public Task AppendInputAudioAsync(string base64Audio, string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.InputAudioAppend(base64Audio, eventId), cancellationToken);
    }

    /// <summary>
    /// Appends raw audio bytes. PCM16 payloads must contain complete 16-bit samples.
    /// </summary>
    public Task AppendInputAudioAsync(byte[] audio, string? eventId = null, CancellationToken cancellationToken = default)
    {
        if (audio.Length == 0)
        {
            throw new ArgumentException("GPT-Live input audio appends must be non-empty.", nameof(audio));
        }

        OpenAiLiveAudioFormat? format = options.Session?.Audio?.Format;
        if ((format?.RequiresEvenByteLength ?? true) && audio.Length % 2 != 0)
        {
            throw new ArgumentException("PCM16 audio must contain an even number of bytes.", nameof(audio));
        }

        return AppendInputAudioAsync(Convert.ToBase64String(audio), eventId, cancellationToken);
    }

    public Task MuteInputAudioAsync(string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.InputAudioMute(eventId), cancellationToken);
    }

    public Task UnmuteInputAudioAsync(string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.InputAudioUnmute(eventId), cancellationToken);
    }

    public Task AppendInstructionsAsync(string content, string? delegationId = null, string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.InstructionsAppend(content, delegationId, eventId), cancellationToken);
    }

    public Task AppendThinkingAsync(string content, string? delegationId = null, string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.ThinkingAppend(content, delegationId, eventId), cancellationToken);
    }

    public Task AppendCommentaryAsync(string content, string? delegationId = null, string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.CommentaryAppend(content, delegationId, eventId), cancellationToken);
    }

    /// <summary>Updates native Decisions questions. Await session.updated before appending items for the new step.</summary>
    public Task UpdateDecisionsAsync(OpenAiLiveDecisionsDelegation decisions, string? eventId = null, CancellationToken cancellationToken = default)
        => SendAsync(OpenAiLiveClientEvents.DecisionsUpdate(decisions, eventId), cancellationToken);

    /// <summary>Updates transcript triggers. An empty list disables them.</summary>
    public Task UpdateTriggersAsync(System.Collections.Generic.List<OpenAiLiveDecisionTrigger> triggers, string? eventId = null, CancellationToken cancellationToken = default)
        => SendAsync(new OpenAiLiveSessionUpdateEvent
        {
            EventId = eventId,
            Session = new OpenAiLiveSessionUpdate { Triggers = triggers }
        }, cancellationToken);

    /// <summary>Appends shared state to Decisions. Await decision.item.created before acting on that state.</summary>
    public Task CreateDecisionItemAsync(object item, string? eventId = null, CancellationToken cancellationToken = default)
        => SendAsync(OpenAiLiveClientEvents.DecisionItemCreate(item, eventId), cancellationToken);

    public Task CreateResponseItemAsync(object item, string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.ResponseItemCreate(item, eventId), cancellationToken);
    }

    public Task SubmitFunctionOutputAsync(string callId, string output, string? eventId = null, CancellationToken cancellationToken = default)
    {
        return CreateResponseItemAsync(OpenAiLiveResponseItems.FunctionCallOutput(callId, output), eventId, cancellationToken);
    }

    public Task CreateResponseAsync(string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.ResponseCreate(eventId), cancellationToken);
    }

    /// <summary>
    /// Sends <c>session.close</c>. Keep the socket open until <see cref="WaitForClosedAsync"/>.
    /// </summary>
    public Task CloseSessionAsync(string? eventId = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(OpenAiLiveClientEvents.SessionClose(eventId), cancellationToken);
    }

    /// <summary>
    /// Waits for <c>session.started</c>. Sideband connections are already running and complete immediately.
    /// </summary>
    public Task<OpenAiLiveSessionStartedEvent> WaitForStartedAsync(CancellationToken cancellationToken = default)
    {
        return WaitAsync(started.Task, cancellationToken);
    }

    internal void MarkSidebandReady()
    {
        started.TrySetResult(new OpenAiLiveSessionStartedEvent
        {
            Type = OpenAiLiveEventTypes.SessionStarted,
            Session = new OpenAiLiveSessionResource { Id = SessionId }
        });
    }

    public Task<OpenAiLiveSessionClosedEvent> WaitForClosedAsync(CancellationToken cancellationToken = default)
    {
        return WaitAsync(closed.Task, cancellationToken);
    }

    /// <summary>
    /// Sends <c>session.close</c> and waits for <c>session.closed</c>.
    /// </summary>
    public async Task<OpenAiLiveSessionClosedEvent?> CloseAndWaitAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));

        if (IsOpen)
        {
            await CloseSessionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await WaitForClosedAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Sends a typed client event.
    /// </summary>
    public Task SendAsync(OpenAiLiveClientEvent clientEvent, CancellationToken cancellationToken = default)
    {
        string json = JsonConvert.SerializeObject(clientEvent, OpenAiLiveJson.Settings);
        return SendRawAsync(json, cancellationToken);
    }

    public async Task SendRawAsync(string json, CancellationToken cancellationToken = default)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
#if MODERN
            await webSocket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
#else
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
#endif
        }
        finally
        {
            sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync()
    {
        byte[] buffer = new byte[1024 * 64];
        StringBuilder sb = new StringBuilder();

        try
        {
            while (webSocket.State == WebSocketState.Open && !linkedCts.IsCancellationRequested)
            {
                sb.Clear();
#if MODERN
                ValueWebSocketReceiveResult result;
#else
                WebSocketReceiveResult result;
#endif

                do
                {
#if MODERN
                    result = await webSocket.ReceiveAsync(buffer.AsMemory(), linkedCts.Token).ConfigureAwait(false);
#else
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), linkedCts.Token).ConfigureAwait(false);
#endif
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        try
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch
                        {
                            // already closing
                        }

#if MODERN
                        options.OnClose?.Invoke(webSocket.CloseStatusDescription);
#else
                        options.OnClose?.Invoke(result.CloseStatusDescription);
#endif
                        return;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                string raw = sb.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                OpenAiLiveServerEvent evt = OpenAiLiveEventParser.Parse(raw);
                Apply(evt);

                if (options.OnEvent is not null)
                {
                    await options.OnEvent(evt).ConfigureAwait(false);
                }

                if (options.Handler is not null)
                {
                    await options.Handler.DispatchAsync(evt).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on dispose
        }
        catch (Exception ex)
        {
            options.OnError?.Invoke(ex);
        }
    }

    private void Apply(OpenAiLiveServerEvent evt)
    {
        switch (evt)
        {
            case OpenAiLiveSessionStartedEvent startedEvent:
                Session = startedEvent.Session;
                SessionId = startedEvent.Session?.Id ?? SessionId;
                started.TrySetResult(startedEvent);
                break;
            case OpenAiLiveSessionUpdatedEvent updatedEvent:
                Session = updatedEvent.Session ?? Session;
                break;
            case OpenAiLiveUsageUpdatedEvent usageEvent:
                Usage = usageEvent.Usage ?? Usage;
                break;
            case OpenAiLiveSessionClosedEvent closedEvent:
                Closed = closedEvent;
                Usage = closedEvent.Usage ?? Usage;
                Session = closedEvent.Session ?? Session;
                closed.TrySetResult(closedEvent);
                break;
            case OpenAiLiveErrorEvent errorEvent:
                if (!IsStarted)
                {
                    started.TrySetException(new OpenAiLiveException(errorEvent.Error?.Message ?? "GPT-Live startup failed.", errorEvent));
                }
                break;
        }
    }

    private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
        {
            return await task.ConfigureAwait(false);
        }

        if (!cancellationToken.CanBeCanceled)
        {
            return await task.ConfigureAwait(false);
        }

        TaskCompletionSource<object?> cancelSignal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(static state =>
        {
            ((TaskCompletionSource<object?>)state!).TrySetResult(null);
        }, cancelSignal);

        Task completed = await Task.WhenAny(task, cancelSignal.Task).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "dispose", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            // ignore close errors
        }

        linkedCts.Cancel();
        if (receiveTask is not null)
        {
            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        webSocket.Dispose();
        sendLock.Dispose();
        linkedCts.Dispose();
    }
}

/// <summary>
/// GPT-Live protocol or startup error.
/// </summary>
public sealed class OpenAiLiveException : Exception
{
    public OpenAiLiveException(string message, OpenAiLiveErrorEvent? error = null) : base(message)
    {
        Error = error;
    }

    public OpenAiLiveErrorEvent? Error { get; }
}

internal static class OpenAiLiveJson
{
    internal static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore
    };
}
