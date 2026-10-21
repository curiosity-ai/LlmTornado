using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Responses;
using LlmTornado.Responses.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests.Responses;

[TestFixture]
public class ResponsesSteeringTests
{
    private static ResponseRequest Request(string text = "Plan the project") => new() { Model = ChatModel.OpenAi.Gpt6.V6Astra, InputString = text, Store = false };

    [Test]
    public void SteeringPayload_ContainsOnlyNativeFieldsAndSupportsMultimodalUserMessages()
    {
        JObject text = new ResponseSteerRequest("resp_1", "Make it smaller").ToPayload();
        Assert.That(text.Properties().Select(x => x.Name), Is.EquivalentTo(new[] { "type", "previous_response_id", "input" }));
        Assert.That(text["input"]?.ToString(), Is.EqualTo("Make it smaller"));
        JObject multimodal = new ResponseSteerRequest("resp_1", [new(ChatMessageRoles.User,
            [new ResponseInputContentText("Use these references"), ResponseInputContentImage.CreateImageUrl("data:image/png;base64,AQ=="), ResponseInputContentFile.CreateFromFileId("file_1")])]).ToPayload();
        Assert.That(multimodal["input"]?[0]?["role"]?.ToString(), Is.EqualTo("user"));
        Assert.That(multimodal["input"]?[0]?["content"]?.Select(x => x["type"]?.ToString()), Is.EqualTo(new[] { "input_text", "input_image", "input_file" }));
        Assert.That(multimodal["input"]?[0]?["content"]?[2]?["file_id"]?.ToString(), Is.EqualTo("file_1"));
        Assert.That(multimodal["stream_id"], Is.Null);
    }

    [Test]
    public void CreatePayload_UsesWebSocketOnlyOptionsWithoutAddingThemToHttpRequests()
    {
        TornadoApi api = new(LLmProviders.OpenAi, "local-key");
        ResponseRequest request = Request();
        request.Stream = true;
        request.Background = true;
        request.StreamOptions = new() { IncludeObfuscation = true };
        JObject body = JObject.Parse(ResponsesWebSocketConnection.BuildCreatePayload(request, api.GetProvider(LLmProviders.OpenAi), new() { StreamId = "warmup", Generate = false }));
        Assert.That(body["stream_id"]?.ToString(), Is.EqualTo("warmup"));
        Assert.That(body["generate"]?.Value<bool>(), Is.False);
        Assert.That(body["stream"], Is.Null);
        Assert.That(body["background"], Is.Null);
        Assert.That(body["stream_options"], Is.Null);
        JObject http = JObject.Parse(request.Serialize(api.GetProvider(LLmProviders.OpenAi)).Body.ToString()!);
        Assert.That(http["stream_id"], Is.Null);
        Assert.That(http["generate"], Is.Null);
    }

    [TestCase("target")]
    [TestCase("empty-array")]
    [TestCase("both")]
    [TestCase("neither")]
    [TestCase("role")]
    [TestCase("audio")]
    [TestCase("status")]
    public void SteeringPayload_RejectsUnsupportedInput(string invalid)
    {
        ResponseSteerRequest request = invalid switch
        {
            "target" => new("", "Update"), "empty-array" => new("resp_1", new List<ResponseInputMessage>()),
            "both" => new("resp_1", "Update") { InputMessages = [new(ChatMessageRoles.User, "Also")] },
            "neither" => new() { PreviousResponseId = "resp_1" },
            "role" => new("resp_1", [new(ChatMessageRoles.System, "Update")]),
            "audio" => new("resp_1", [new(ChatMessageRoles.User, [new ResponseInputContentAudio()])]),
            _ => new("resp_1", [new(ChatMessageRoles.User, "Update") { Status = ResponseMessageStatuses.Completed }])
        };
        Assert.Throws<ArgumentException>(() => request.ToPayload());
    }

    [TestCase("response.incomplete", "steered")]
    [TestCase("response.completed", null)]
    public async Task Create_FollowsAutomaticSteeringSuccessorAfterOriginalTerminal(string terminalType, string? reason)
    {
        await using Pair pair = await Pair.Open();
        List<string> created = [];
        List<ResponseWebSocketEvent> envelopes = [];
        bool accepted = false;
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), new()
        {
            OnWebSocketEvent = e => { envelopes.Add(e); return ValueTask.CompletedTask; },
            OnResponseCreated = async e =>
            {
                Assert.That(pair.Connection.ActiveResponseId, Is.EqualTo(e.Response.Id));
                created.Add(e.Response.Id);
                if (created.Count == 1) await pair.Connection.SteerAsync(e.Response.Id, "Two weeks only", pair.Timeout.Token);
            },
            OnResponseSteerAccepted = _ =>
            {
                accepted = true;
                Assert.That(pair.Connection.SteeringSubmissions.Single().Status, Is.EqualTo(ResponseSteerSubmissionStatus.Accepted));
                return ValueTask.CompletedTask;
            }
        }, cancellationToken: pair.Timeout.Token);
        Assert.That((await pair.Receive())["type"]?.ToString(), Is.EqualTo("response.create"));
        await pair.Send(Lifecycle("response.created", "resp_1", "in_progress"));
        JObject steer = await pair.Receive();
        Assert.That(steer["type"]?.ToString(), Is.EqualTo("response.steer"));
        Assert.That(steer.Properties().Count(), Is.EqualTo(3));
        await pair.Send(Steer("response.steer.accepted", "steer_1", "resp_1"));
        await pair.Send(Lifecycle(terminalType, "resp_1", terminalType == "response.completed" ? "completed" : "incomplete", reason: reason));
        await pair.Send(Lifecycle("response.created", "resp_2", "in_progress", "resp_1"));
        string answer = "Updated plan: " + new string('ž', 6000);
        await pair.Send(Lifecycle("response.completed", "resp_2", "completed", "resp_1", answer), fragment: true);
        ResponseResult? result = await operation.WaitAsync(pair.Timeout.Token);
        Assert.That(result?.Id, Is.EqualTo("resp_2"));
        Assert.That(result?.OutputText, Is.EqualTo(answer));
        Assert.That(accepted, Is.True);
        Assert.That(created, Is.EqualTo(new[] { "resp_1", "resp_2" }));
        Assert.That(envelopes.Select(x => x.Type), Does.Contain("response.steer.accepted"));
        Assert.That(pair.Connection.CurrentResponseId, Is.EqualTo("resp_2"));
        Assert.That(pair.Connection.SteeringSubmissions.Single().Status, Is.EqualTo(ResponseSteerSubmissionStatus.Committed));
        Assert.That(pair.Connection.SteeringSubmissions.Single().SuccessorResponseId, Is.EqualTo("resp_2"));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task PendingSteering_ReturnsForToolInputOrContinuesWhenCallbackSendsIt(bool resumeInCallback, bool multipleSubmissions)
    {
        await using Pair pair = await Pair.Open();
        ResponseRequest ToolResult() => new()
        {
            Model = ChatModel.OpenAi.Gpt6.V6Luna, Instructions = new StringResponseInstruction("Use the saved tool result"),
            InputItems = [new FunctionToolCallOutput("call_1", "Saved result")], PreviousResponseId = "resp_1"
        };
        var options = new ResponseWebSocketCreateOptions { StreamId = "work" };
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), new()
        {
            OnResponseCreated = async e =>
            {
                if (e.Response.Id != "resp_1") return;
                await pair.Connection.SteerAsync("resp_1", "Use two weeks", pair.Timeout.Token);
                if (multipleSubmissions) await pair.Connection.SteerAsync("resp_1", "Keep the team small", pair.Timeout.Token);
            },
            OnResponseSteerPending = async e =>
            {
                Assert.That(e.RequiredInput.Single().CallId, Is.EqualTo("call_1"));
                if (resumeInCallback) await pair.Connection.SendResponseAsync(ToolResult(), options, pair.Timeout.Token);
            }
        }, options, pair.Timeout.Token);
        Assert.That((await pair.Receive())["stream_id"]?.ToString(), Is.EqualTo("work"));
        await pair.Send(Lifecycle("response.created", "resp_1", "in_progress", lane: "work"));
        Assert.That((await pair.Receive())["stream_id"], Is.Null, "A steering target determines its lane.");
        if (multipleSubmissions) Assert.That((await pair.Receive())["input"]?.ToString(), Is.EqualTo("Keep the team small"));
        await pair.Send(Steer("response.steer.accepted", "steer_1", "resp_1", "work"));
        if (multipleSubmissions) await pair.Send(Steer("response.steer.accepted", "steer_2", "resp_1", "work"));
        JObject completed = Lifecycle("response.completed", "resp_1", "completed", lane: "work");
        completed["response"]!["output"] = JArray.Parse("""[{"type":"function_call","id":"fc_1","call_id":"call_1","name":"lookup","arguments":"{}","status":"completed"}]""");
        await pair.Send(completed);
        JObject pending = Steer("response.steer.pending", "steer_1", "resp_1", "work");
        pending["reason"] = "waiting_for_required_input";
        pending["required_input"] = JArray.Parse("""[{"type":"function_call_output","call_id":"call_1","name":"lookup"}]""");
        await pair.Send(pending);
        if (multipleSubmissions)
        {
            JObject second = (JObject)pending.DeepClone();
            second["steer"]!["id"] = "steer_2";
            await pair.Send(second);
        }
        if (!resumeInCallback)
        {
            Assert.That((await operation.WaitAsync(pair.Timeout.Token))?.Id, Is.EqualTo("resp_1"));
            Assert.That(pair.Connection.PendingSteers.Single().Steer.Id, Is.EqualTo("steer_1"));
            Assert.That(pair.Connection.SteeringSubmissions[0].Status, Is.EqualTo(ResponseSteerSubmissionStatus.WaitingForInput));
            operation = pair.Connection.CreateResponseAsync(ToolResult(), options: options, cancellationToken: pair.Timeout.Token);
        }
        JObject continuation = await pair.Receive();
        Assert.That(continuation["type"]?.ToString(), Is.EqualTo("response.create"));
        Assert.That(continuation["previous_response_id"]?.ToString(), Is.EqualTo("resp_1"));
        Assert.That(continuation["stream_id"]?.ToString(), Is.EqualTo("work"));
        Assert.That(continuation["model"]?.ToString(), Is.EqualTo("gpt-6-luna"));
        Assert.That(continuation["input"]?.Count(), Is.EqualTo(1));
        Assert.That(continuation["input"]?[0]?["output"]?.ToString(), Is.EqualTo("Saved result"));
        await pair.Send(Lifecycle("response.created", "resp_2", "in_progress", "resp_1", lane: "work"));
        await pair.Send(Lifecycle("response.completed", "resp_2", "completed", "resp_1", "Updated", "work"));
        Assert.That((await operation.WaitAsync(pair.Timeout.Token))?.Id, Is.EqualTo("resp_2"));
        Assert.That(pair.Connection.PendingSteers, Is.Empty);
        Assert.That(pair.Connection.SteeringSubmissions.Count, Is.EqualTo(multipleSubmissions ? 2 : 1));
        Assert.That(pair.Connection.SteeringSubmissions.All(x => x.Status == ResponseSteerSubmissionStatus.Committed), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SteeringFailure_PreservesReturnedInputAndDoesNotInventAContinuation(bool accepted)
    {
        await using Pair pair = await Pair.Open();
        ResponseEventSteerFailed? failure = null;
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), new()
        {
            OnResponseCreated = async _ => await pair.Connection.SteerAsync("resp_1", "Update", pair.Timeout.Token),
            OnResponseSteerFailed = e => { failure = e; return ValueTask.CompletedTask; }
        }, cancellationToken: pair.Timeout.Token);
        await pair.Receive();
        await pair.Send(Lifecycle("response.created", "resp_1", "in_progress"));
        await pair.Receive();
        if (accepted) await pair.Send(Steer("response.steer.accepted", "steer_1", "resp_1"));
        await pair.Send(Lifecycle("response.completed", "resp_1", "completed", text: "Original"));
        JObject rejected = Steer("response.steer.failed", accepted ? "steer_1" : null, "resp_1");
        rejected["steer"]!["input"] = "Update";
        rejected["error"] = JObject.Parse("""{"type":"invalid_request_error","code":"future_failure","message":"Input could not be applied"}""");
        await pair.Send(rejected);
        Assert.That((await operation.WaitAsync(pair.Timeout.Token))?.Id, Is.EqualTo("resp_1"));
        Assert.That(failure?.Error.Code, Is.EqualTo("future_failure"));
        Assert.That(failure?.Steer.Input?.ToString(), Is.EqualTo("Update"));
        Assert.That(pair.Connection.SteeringSubmissions.Single().Status, Is.EqualTo(ResponseSteerSubmissionStatus.Failed));
    }

    [TestCase("response.incomplete", "incomplete")]
    [TestCase("response.failed", "failed")]
    public async Task TerminalResponseWithoutHandler_ReturnsTheFinalObject(string type, string status)
    {
        await using Pair pair = await Pair.Open();
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), cancellationToken: pair.Timeout.Token);
        await pair.Receive();
        await pair.Send(Lifecycle("response.created", "resp_1", "in_progress"));
        JObject terminal = Lifecycle(type, "resp_1", status, reason: "max_output_tokens");
        if (status == "failed") terminal["response"]!["error"] = JObject.Parse("""{"code":"server_error","message":"Failure"}""");
        await pair.Send(terminal);
        ResponseResult? result = await operation.WaitAsync(pair.Timeout.Token);
        Assert.That(result?.Status.ToString().ToLowerInvariant(), Is.EqualTo(status));
        Assert.That(pair.Connection.CurrentResponse, Is.SameAs(result));
    }

    [Test]
    public async Task LowLevelReader_RoutesInterleavedLanesAndPreservesUnknownEvents()
    {
        await using Pair pair = await Pair.Open();
        Task<List<ResponseWebSocketEvent>> reading = Task.Run(async () =>
        {
            List<ResponseWebSocketEvent> events = [];
            await foreach (var evt in pair.Connection.ReadEventsAsync(cancellationToken: pair.Timeout.Token))
            {
                events.Add(evt);
                if (events.Count == 5) break;
            }
            return events;
        });
        await pair.Connection.SendResponseAsync(Request("A"), new() { StreamId = "A" }, pair.Timeout.Token);
        await pair.Connection.SendResponseAsync(Request("B"), new() { StreamId = "B" }, pair.Timeout.Token);
        Assert.That((await pair.Receive())["stream_id"]?.ToString(), Is.EqualTo("A"));
        Assert.That((await pair.Receive())["stream_id"]?.ToString(), Is.EqualTo("B"));
        await pair.Send(Lifecycle("response.created", "resp_B", "in_progress", lane: "B"));
        await pair.Send(Lifecycle("response.created", "resp_A", "in_progress", lane: "A"));
        await pair.Send(JObject.Parse("""{"type":"response.future_event","stream_id":"B","opaque":{"value":7}}"""));
        await pair.Send(Lifecycle("response.completed", "resp_A", "completed", lane: "A"));
        await pair.Send(Lifecycle("response.completed", "resp_B", "completed", lane: "B"));
        var events = await reading.WaitAsync(pair.Timeout.Token);
        Assert.That(events.Select(x => x.StreamId), Is.EqualTo(new[] { "B", "A", "B", "A", "B" }));
        Assert.That(events[2].Event, Is.Null);
        Assert.That(events[2].Raw["opaque"]?["value"]?.Value<int>(), Is.EqualTo(7));
        ResponseRequest next = Request("Next A");
        await pair.Connection.SendResponseAsync(next, new() { StreamId = "A" }, pair.Timeout.Token);
        Assert.That((await pair.Receive())["previous_response_id"]?.ToString(), Is.EqualTo("resp_A"));
        await pair.Connection.SendResponseAsync(Request("New independent turn"), new() { StreamId = "A", ContinueFromCurrentResponse = false }, pair.Timeout.Token);
        Assert.That((await pair.Receive())["previous_response_id"], Is.Null);
        Assert.That(pair.Connection.GetCurrentResponse("B")?.Id, Is.EqualTo("resp_B"));
    }

    [Test]
    public async Task ConcurrentReaders_AreRejectedBeforeSendingAnotherCreate()
    {
        await using Pair pair = await Pair.Open();
        Task<ResponseResult?> first = pair.Connection.CreateResponseAsync(Request(), cancellationToken: pair.Timeout.Token);
        await pair.Receive();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await pair.Connection.CreateResponseAsync(Request(), cancellationToken: pair.Timeout.Token));
        await using var reader = pair.Connection.ReadEventsAsync(cancellationToken: pair.Timeout.Token).GetAsyncEnumerator();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.MoveNextAsync());
        await pair.Send(Lifecycle("response.completed", "resp_1", "completed"));
        Assert.That((await first.WaitAsync(pair.Timeout.Token))?.Id, Is.EqualTo("resp_1"));
    }

    [Test]
    public async Task DisconnectAfterAcceptance_LeavesUncommittedInputUnknown()
    {
        int closed = 0;
        await using Pair pair = await Pair.Open(new() { OnClose = () => closed++ });
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), new()
        {
            OnResponseCreated = async _ => await pair.Connection.SteerAsync("resp_1", "Update", pair.Timeout.Token)
        }, cancellationToken: pair.Timeout.Token);
        await pair.Receive();
        await pair.Send(Lifecycle("response.created", "resp_1", "in_progress"));
        await pair.Receive();
        await pair.Send(Steer("response.steer.accepted", "steer_1", "resp_1"));
        await pair.Send(Lifecycle("response.incomplete", "resp_1", "incomplete", reason: "steered"));
        await pair.Peer.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, pair.Timeout.Token);
        Assert.ThrowsAsync<EndOfStreamException>(async () => await operation.WaitAsync(pair.Timeout.Token));
        Assert.That(pair.Connection.SteeringSubmissions.Single().Status, Is.EqualTo(ResponseSteerSubmissionStatus.Unknown));
        Assert.That(pair.Connection.SteeringSubmissions.Single().Input?.ToString(), Is.EqualTo("Update"));
        Assert.That(closed, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancellationAndDisposal_InterruptTheActiveReader(bool disposeFirst)
    {
        await using Pair pair = await Pair.Open();
        using CancellationTokenSource cancel = new();
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), cancellationToken: cancel.Token);
        await pair.Receive();
        if (disposeFirst) await pair.Connection.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        else cancel.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(3)));
        await pair.Connection.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task CrossLaneFork_DoesNotKeepTheParentsCreateOperationReading()
    {
        await using Pair pair = await Pair.Open();
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), new()
        {
            OnResponseCompleted = async e => await pair.Connection.SendResponseAsync(new ResponseRequest
            {
                Model = ChatModel.OpenAi.Gpt6.V6Astra, PreviousResponseId = e.Response.Id, InputString = "Fork"
            }, new() { StreamId = "fork" }, pair.Timeout.Token)
        }, new() { StreamId = "parent" }, pair.Timeout.Token);
        await pair.Receive();
        await pair.Send(Lifecycle("response.created", "resp_parent", "in_progress", lane: "parent"));
        await pair.Send(Lifecycle("response.completed", "resp_parent", "completed", lane: "parent"));
        JObject fork = await pair.Receive();
        Assert.That(fork["stream_id"]?.ToString(), Is.EqualTo("fork"));
        Assert.That(fork["previous_response_id"]?.ToString(), Is.EqualTo("resp_parent"));
        Assert.That((await operation.WaitAsync(pair.Timeout.Token))?.Id, Is.EqualTo("resp_parent"));
    }

    [Test]
    public async Task PendingEvent_DispatchesAllRequiredInputKindsAndFutureFields()
    {
        TornadoApi api = new(LLmProviders.OpenAi, "local-key");
        ResponseEventSteerPending? pending = null;
        List<ResponseEventTypes> general = [];
        JObject evt = Steer("response.steer.pending", "steer_1", "resp_1", "work");
        evt["reason"] = "future_reason";
        evt["required_input"] = JArray.Parse("""[{"type":"function_call_output","call_id":"fn","name":"lookup"},{"type":"custom_tool_call_output","call_id":"custom"},{"type":"computer_call_output","call_id":"computer"},{"type":"shell_call_output","call_id":"shell"},{"type":"apply_patch_call_output","call_id":"patch"},{"type":"tool_search_output","call_id":"search","execution":"client"},{"type":"mcp_approval_response","approval_request_id":"approval_1"},{"type":"future_input","extra":7}]""");
        await api.Responses.DispatchResponseStreamEventAsync("response.steer.pending", evt.ToString(Formatting.None), null, new()
        {
            OnEvent = e => { general.Add(e.EventType); return ValueTask.CompletedTask; },
            OnResponseSteerPending = e => { pending = e; return ValueTask.CompletedTask; }
        }, CancellationToken.None);
        Assert.That(general, Is.EqualTo(new[] { ResponseEventTypes.ResponseSteerPending }));
        Assert.That(pending?.Reason, Is.EqualTo("future_reason"));
        Assert.That(pending?.RequiredInput.Count, Is.EqualTo(8));
        Assert.That(pending?.RequiredInput[5].Execution, Is.EqualTo("client"));
        Assert.That(pending?.RequiredInput[6].ApprovalRequestId, Is.EqualTo("approval_1"));
        Assert.That(pending?.RequiredInput[7].AdditionalData?["extra"]?.Value<int>(), Is.EqualTo(7));
    }

    [TestCase("response.failed", "failed")]
    [TestCase("response.incomplete", "incomplete")]
    public async Task SharedDispatcher_UpdatesSessionForAllTerminalResponses(string type, string status)
    {
        TornadoApi api = new(LLmProviders.OpenAi, "local-key");
        ResponsesSession session = new() { Endpoint = api.Responses, CurrentResponse = new() { Id = "old" } };
        bool terminal = await api.Responses.DispatchResponseStreamEventAsync(type, Lifecycle(type, "resp_terminal", status).ToString(Formatting.None), session, null, CancellationToken.None);
        Assert.That(terminal, Is.True);
        Assert.That(session.CurrentResponse.Id, Is.EqualTo("resp_terminal"));
        Assert.That(session.CurrentResponse.Status.ToString().ToLowerInvariant(), Is.EqualTo(status));
    }

    [Test]
    public async Task CallbackContinuation_UsesItsOwnClientToolDefinitions()
    {
        await using Pair pair = await Pair.Open();
        int originalCalls = 0, continuationCalls = 0;
        ResponseRequest initial = Request();
        initial.Tools = [Function(() => originalCalls++)];
        ResponseRequest continuation = Request();
        continuation.PreviousResponseId = "resp_1";
        continuation.InputString = null;
        continuation.InputItems = [new FunctionToolCallOutput("call_1", "Saved result")];
        continuation.Tools = [Function(() => continuationCalls++)];
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(initial, new()
        {
            OnResponseCreated = async e => { if (e.Response.Id == "resp_1") await pair.Connection.SteerAsync("resp_1", "Update", pair.Timeout.Token); },
            OnResponseSteerPending = async _ => await pair.Connection.SendResponseAsync(continuation, cancellationToken: pair.Timeout.Token)
        }, cancellationToken: pair.Timeout.Token);
        await pair.Receive();
        await pair.Send(Lifecycle("response.created", "resp_1", "in_progress"));
        await pair.Receive();
        await pair.Send(Steer("response.steer.accepted", "steer_1", "resp_1"));
        await pair.Send(FunctionCompleted("resp_1", null));
        JObject pending = Steer("response.steer.pending", "steer_1", "resp_1");
        pending["reason"] = "waiting_for_required_input";
        pending["required_input"] = JArray.Parse("""[{"type":"function_call_output","call_id":"call_1","name":"lookup"}]""");
        await pair.Send(pending);
        await pair.Receive();
        await pair.Send(Lifecycle("response.created", "resp_2", "in_progress", "resp_1"));
        await pair.Send(FunctionCompleted("resp_2", "resp_1"));
        Assert.That((await operation.WaitAsync(pair.Timeout.Token))?.Id, Is.EqualTo("resp_2"));
        Assert.That(originalCalls, Is.Zero);
        Assert.That(continuationCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task QueuedRequest_DoesNotReplaceTheCurrentResponsesClientSettings()
    {
        await using Pair pair = await Pair.Open();
        int originalCalls = 0, queuedCalls = 0;
        ResponseRequest initial = Request();
        initial.Tools = [Function(() => originalCalls++)];
        ResponseRequest queued = Request();
        queued.Tools = [Function(() => queuedCalls++)];
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(initial, new()
        {
            OnResponseCreated = async _ => await pair.Connection.SendResponseAsync(queued, new() { ContinueFromCurrentResponse = false }, pair.Timeout.Token)
        }, cancellationToken: pair.Timeout.Token);
        await pair.Receive();
        await pair.Send(Lifecycle("response.created", "resp_1", "in_progress"));
        await pair.Receive();
        await pair.Send(FunctionCompleted("resp_1", null));
        Assert.That((await operation.WaitAsync(pair.Timeout.Token))?.Id, Is.EqualTo("resp_1"));
        Assert.That(originalCalls, Is.EqualTo(1));
        Assert.That(queuedCalls, Is.Zero);
    }

    private static ResponseTool Function(Action action)
    {
        Tool tool = new(action, "lookup");
        tool.Serialize(new TornadoApi(LLmProviders.OpenAi, "local-key").GetProvider(LLmProviders.OpenAi));
        return ResponseHelpers.ToResponseTool(tool)!;
    }

    private static JObject FunctionCompleted(string id, string? parent)
    {
        JObject evt = Lifecycle("response.completed", id, "completed", parent);
        evt["response"]!["output"] = JArray.Parse("""[{"type":"function_call","id":"fc_1","call_id":"call_1","name":"lookup","arguments":"{}","status":"completed"}]""");
        return evt;
    }

    [Test]
    public async Task WebSocketProtocolErrors_PreserveNestedDetailsAndLegacyAccessors()
    {
        await using Pair pair = await Pair.Open();
        ResponseEventError? error = null;
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), new()
        {
            OnResponseError = e => { error = e; return ValueTask.CompletedTask; }
        }, new() { StreamId = "lane" }, pair.Timeout.Token);
        await pair.Receive();
        await pair.Send(JObject.Parse("""{"type":"error","stream_id":"lane","status":429,"error":{"type":"rate_limit_error","code":"future_code","message":"Slow down","param":"stream_id","headers":{"retry-after":"3"},"future_detail":7}}"""));
        Assert.That(await operation.WaitAsync(pair.Timeout.Token), Is.Null);
        Assert.That(error?.Code, Is.EqualTo("future_code"));
        Assert.That(error?.Message, Is.EqualTo("Slow down"));
        Assert.That(error?.Error?.Headers?["retry-after"], Is.EqualTo("3"));
        Assert.That(error?.Error?.AdditionalData?["future_detail"]?.Value<int>(), Is.EqualTo(7));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SafeCreate_ReportsTransportFailure(bool binaryMessage)
    {
        await using Pair pair = await Pair.Open();
        Exception? failure = null;
        Task<ResponseResult?> operation = pair.Connection.CreateResponseSafeAsync(Request(), new()
        {
            OnException = e => { failure = e.Exception; return ValueTask.CompletedTask; }
        }, cancellationToken: pair.Timeout.Token);
        await pair.Receive();
        if (binaryMessage) await pair.Peer.SendAsync(new byte[] { 1, 2 }, WebSocketMessageType.Binary, true, pair.Timeout.Token);
        else await pair.Peer.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, pair.Timeout.Token);
        Assert.That(await operation.WaitAsync(pair.Timeout.Token), Is.Null);
        Assert.That(failure, Is.Not.Null);
    }

    internal static JObject Lifecycle(string type, string id, string status, string? previous = null, string? text = null, string? lane = null, string? reason = null)
    {
        JObject response = new() { ["id"] = id, ["object"] = "response", ["status"] = status, ["model"] = "gpt-6-astra", ["output"] = new JArray() };
        if (previous is not null) response["previous_response_id"] = previous;
        if (reason is not null) response["incomplete_details"] = new JObject { ["reason"] = reason };
        if (text is not null) response["output"] = new JArray(new JObject { ["type"] = "message", ["role"] = "assistant", ["id"] = "msg_1", ["status"] = "completed", ["content"] = new JArray(new JObject { ["type"] = "output_text", ["text"] = text, ["annotations"] = new JArray() }) });
        JObject evt = new() { ["type"] = type, ["response"] = response };
        if (lane is not null) evt["stream_id"] = lane;
        return evt;
    }

    private static JObject Steer(string type, string? id, string parent, string? lane = null)
    {
        JObject steer = new() { ["previous_response_id"] = parent };
        if (id is not null) steer["id"] = id;
        JObject evt = new() { ["type"] = type, ["sequence_number"] = 2, ["steer"] = steer };
        if (lane is not null) evt["stream_id"] = lane;
        return evt;
    }

    internal sealed class Pair : IAsyncDisposable
    {
        internal CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(20));
        private readonly HttpListener listener = new();
        internal ResponsesWebSocketConnection Connection { get; private set; } = null!;
        internal string? BetaHeader { get; private set; }
        internal WebSocket Peer { get; private set; } = null!;
        internal static async Task<Pair> Open(ResponsesWebSocketConnectOptions? options = null)
        {
            Pair pair = new();
            using TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            pair.listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            pair.listener.Start();
            options ??= new();
            options.UrlOverride = $"ws://127.0.0.1:{port}/v1/responses";
            Task<ResponsesWebSocketConnection> connecting = new TornadoApi(LLmProviders.OpenAi, "local-key").Responses.ConnectWebSocketAsync(options, pair.Timeout.Token);
            var context = await pair.listener.GetContextAsync().WaitAsync(pair.Timeout.Token);
            Assert.That(context.Request.Headers["Authorization"], Is.EqualTo("Bearer local-key"));
            pair.BetaHeader = context.Request.Headers["OpenAI-Beta"];
            pair.Peer = (await context.AcceptWebSocketAsync(null)).WebSocket;
            pair.Connection = await connecting.WaitAsync(pair.Timeout.Token);
            return pair;
        }
        internal async Task<JObject> Receive()
        {
            byte[] buffer = new byte[4096];
            using MemoryStream message = new();
            WebSocketReceiveResult result;
            do
            {
                result = await Peer.ReceiveAsync(buffer, Timeout.Token);
                Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text));
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            return JObject.Parse(Encoding.UTF8.GetString(message.ToArray()));
        }
        internal async Task Send(JObject value, bool fragment = false)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value.ToString(Formatting.None));
            if (fragment)
            {
                await Peer.SendAsync(new ArraySegment<byte>(bytes, 0, 41), WebSocketMessageType.Text, false, Timeout.Token);
                await Peer.SendAsync(new ArraySegment<byte>(bytes, 41, bytes.Length - 41), WebSocketMessageType.Text, true, Timeout.Token);
            }
            else await Peer.SendAsync(bytes, WebSocketMessageType.Text, true, Timeout.Token);
        }
        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            Peer.Abort();
            Peer.Dispose();
            listener.Close();
            Timeout.Dispose();
        }
    }
}
