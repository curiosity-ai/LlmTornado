using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Responses;
using LlmTornado.Responses.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pair = LlmTornado.Tests.Responses.ResponsesSteeringTests.Pair;
using static LlmTornado.Tests.Responses.ResponsesSteeringTests;

namespace LlmTornado.Tests.Responses;

[TestFixture]
public class ResponsesMultiAgentTests
{
    private static ResponseRequest Request() => new()
    {
        Model = "gpt-6.1-sol", InputString = "Review this project", MultiAgent = new() { Enabled = true }
    };

    [TestCase(null)]
    [TestCase(3)]
    [TestCase(10000)]
    public void Configuration_SerializesNativeFieldsAndCopiesHeaderOptions(int? concurrency)
    {
        TornadoApi api = new(LLmProviders.OpenAi, "local-key");
        ResponseRequest request = Request();
        request.MultiAgent!.MaxConcurrentSubagents = concurrency;
        request.ApiOptions = new() { Betas = ["other=v2, responses_multi_agent=v1", "other=v2"] };
        TornadoRequestContent content = request.Serialize(api.GetProvider(LLmProviders.OpenAi), new() { IncludeHeaders = true });
        JObject body = JObject.Parse(content.Body.ToString()!);
        Assert.That(body["multi_agent"]?["enabled"]?.Value<bool>(), Is.True);
        Assert.That(body["multi_agent"]?["max_concurrent_subagents"]?.Value<int?>(), Is.EqualTo(concurrency));
        Assert.That(body["ApiOptions"], Is.Null);
        Assert.That(string.Join(",", content.Headers!["OpenAI-Beta"]), Is.EqualTo("other=v2,responses_multi_agent=v1"));
        ResponseRequest copy = new(request);
        copy.MultiAgent!.Enabled = false;
        copy.ApiOptions!.Betas!.Add("copy=v1");
        Assert.That(request.MultiAgent.Enabled, Is.True);
        Assert.That(request.ApiOptions.Betas, Has.Count.EqualTo(2));
    }

    [TestCase("summary")]
    [TestCase("tool-limit")]
    public void Configuration_RejectsUnsupportedRequestCombinations(string option)
    {
        ResponseRequest request = Request();
        if (option == "summary") request.Reasoning = new() { Summary = ResponseReasoningSummaries.Auto };
        else request.MaxToolCalls = 2;
        Assert.Throws<ArgumentException>(() => request.Serialize(new TornadoApi(LLmProviders.OpenAi, "local-key").GetProvider(LLmProviders.OpenAi)));
    }

    [Test]
    public async Task HostedOutput_RoundTripsHistoryAttributionAndOpaqueMessagesWithoutClientExecution()
    {
        ResponseResult result = JsonConvert.DeserializeObject<ResponseResult>("""
            {"id":"resp_1","multi_agent":{"enabled":true},"output":[
            {"id":"ma_1","type":"multi_agent_call","action":"future_action","call_id":"call_hosted","arguments":"{\"name\":\"reviewer\"}","agent":{"agent_name":"/root"},"future":7},
            {"id":"mao_1","type":"multi_agent_call_output","action":"future_action","call_id":"call_hosted","output":[{"type":"output_text","text":"Ready","annotations":[],"logprobs":[]}],"agent":{"agent_name":"/root"}},
            {"id":"am_1","type":"agent_message","author":"/root","recipient":"/root/reviewer","content":[{"type":"input_text","text":"Review"},{"type":"encrypted_content","encrypted_content":"opaque+/=="}],"agent":{"agent_name":"/root/reviewer"}},
            {"id":"msg_child","type":"message","role":"assistant","content":[{"type":"output_text","text":"Child report"}],"agent":{"agent_name":"/root/reviewer"}},
            {"id":"msg_root","type":"message","role":"assistant","content":[{"type":"output_text","text":"Final report"}],"agent":{"agent_name":"/root"}},
            {"id":"compact_1","type":"compaction","encrypted_content":"compact+/==","agent":{"agent_name":"/root/reviewer"}}
            ]}
            """)!;
        Assert.That(result.OutputText, Is.EqualTo("Final report"));
        Assert.That(result.MultiAgent?.Enabled, Is.True);
        var message = (ResponseAgentMessageItem)result.Output![2];
        Assert.That(message.Author, Is.EqualTo("/root"));
        Assert.That(message.Agent!.AgentName, Is.EqualTo(message.Recipient));
        Assert.That(((ResponseInputContentEncrypted)message.Content[1]).EncryptedContent, Is.EqualTo("opaque+/=="));
        List<ResponseInputItem> input = result.Output.ToInputItems();
        JArray replay = JArray.FromObject(input);
        Assert.That(replay[0]["future"]?.Value<int>(), Is.EqualTo(7));
        Assert.That(replay[1]["output"]?[0]?["logprobs"], Is.TypeOf<JArray>());
        Assert.That(replay[2]["content"]?[1]?["encrypted_content"]?.ToString(), Is.EqualTo("opaque+/=="));
        Assert.That(replay[3]["agent"]?["agent_name"]?.ToString(), Is.EqualTo("/root/reviewer"));
        Assert.That(replay[5]["agent"]?["agent_name"]?.ToString(), Is.EqualTo("/root/reviewer"));
        Assert.That(JArray.FromObject(replay.ToObject<List<ResponseInputItem>>()!), Is.EqualTo(replay));
        Assert.That(new ResponseRequest { InputItems = input }.RequiresMultiAgentBeta, Is.True);
        Assert.Throws<InvalidOperationException>(() => input.PruneBeforeLatestCompaction());
        Assert.Throws<InvalidOperationException>(() => new ResponseCompactRequest { InputItems = input }.Serialize());
        bool invoked = false;
        await ResponsesEndpoint.HandleResponse(new() { Tools = [new ResponseFunctionTool { Name = "future_action", Delegate = (Action)(() => invoked = true) }] }, result);
        Assert.That(invoked, Is.False);
    }

    [Test]
    public async Task Attribution_PreservesClientCallsAndStreamingIdentity()
    {
        TornadoApi api = new(LLmProviders.OpenAi, "local-key");
        ResponseResult result = JsonConvert.DeserializeObject<ResponseResult>("""
            {"output":[{"type":"function_call","id":"fc_1","call_id":"call_client","name":"lookup","arguments":"{}","agent":{"agent_name":"/root/reviewer"}}]}
            """)!;
        int calls = 0;
        await ResponsesEndpoint.HandleResponse(new() { Tools = [Function(() => calls++)] }, result);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(result.Output!.ToInputItems()[0].Agent!.AgentName, Is.EqualTo("/root/reviewer"));
        ResponseAgent? agent = null;
        await api.Responses.DispatchResponseStreamEventAsync("response.output_text.delta",
            """{"type":"response.output_text.delta","sequence_number":1,"item_id":"msg_1","output_index":1,"content_index":0,"delta":"Report","agent":{"agent_name":"/root/reviewer"}}""",
            null, new() { OnResponseOutputTextDelta = e => { agent = e.Agent; return ValueTask.CompletedTask; } }, CancellationToken.None);
        Assert.That(agent?.AgentName, Is.EqualTo("/root/reviewer"));
        foreach (Type type in typeof(ResponseResult).Assembly.GetTypes().Where(t => t.IsClass && typeof(IResponseOutputItem).IsAssignableFrom(t)))
            Assert.That(typeof(IResponseAgentItem).IsAssignableFrom(type), Is.True, type.Name);
    }

    [TestCase("create")]
    [TestCase("stream")]
    [TestCase("count")]
    [TestCase("retrieve")]
    [TestCase("cancel")]
    [TestCase("delete")]
    [TestCase("input-list")]
    public async Task HttpOperations_ApplyBetaHeaderWithoutPuttingItInTheBody(string action)
    {
        using LocalHttp server = new();
        ResponseApiOptions options = new() { EnableMultiAgent = true, Betas = ["other=v2"] };
        ResponseRequest request = Request();
        request.ApiOptions = options;
        Task operation = action switch
        {
            "create" => server.Api.Responses.CreateResponseSafe(request),
            "stream" => server.Api.Responses.StreamResponseRich(request, token: server.Timeout.Token),
            "count" => server.Api.Responses.CountInputTokensSafe(request, server.Timeout.Token),
            "retrieve" => server.Api.Responses.GetResponseSafe("resp_1", options, server.Timeout.Token),
            "cancel" => server.Api.Responses.CancelResponseSafe("resp_1", options, server.Timeout.Token),
            "delete" => server.Api.Responses.DeleteResponseSafe("resp_1", options, server.Timeout.Token),
            _ => server.Api.Responses.ListResponseInputItemsSafe("resp_1", options, cancellationToken: server.Timeout.Token)
        };
        string response = action == "stream" ? "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"resp_1\",\"status\":\"completed\"}}\n\n" : action == "input-list" ? "{\"data\":[]}" : "{\"id\":\"resp_1\",\"status\":\"completed\"}";
        await server.Reply(response, action == "stream" ? "text/event-stream" : "application/json");
        await operation.WaitAsync(server.Timeout.Token);
        Assert.That(server.Beta, Is.EqualTo("other=v2,responses_multi_agent=v1"));
        if (server.Body.Length > 0) Assert.That(JObject.Parse(server.Body)["ApiOptions"], Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Injection_WaitsForEveryAcknowledgementEvenAfterTerminalAndDoesNotRerunTools(bool lane)
    {
        await using Pair pair = await Pair.Open(new() { EnableMultiAgent = true });
        Assert.That(pair.BetaHeader, Is.EqualTo(ResponseApiOptions.MultiAgentBeta));
        int calls = 0, accepted = 0, failed = 0;
        ResponseRequest request = Request();
        request.Tools = [Function(() => calls++)];
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(request, new()
        {
            OnResponseCreated = async e =>
            {
                await pair.Connection.InjectAsync(new(e.Response.Id, [new FunctionToolCallOutput("call_1", "Saved one")]), pair.Timeout.Token);
                await pair.Connection.InjectAsync(new(e.Response.Id, [new FunctionToolCallOutput("call_2", "Saved two")]), pair.Timeout.Token);
            },
            OnResponseInjectCreated = _ => { accepted++; return ValueTask.CompletedTask; },
            OnResponseInjectFailed = _ => { failed++; return ValueTask.CompletedTask; }
        }, new() { StreamId = lane ? "work" : null }, pair.Timeout.Token);
        await pair.Receive();
        JObject created = Lifecycle("response.created", "resp_1", "in_progress");
        if (lane) created["stream_id"] = "work";
        await pair.Send(created);
        JObject first = await pair.Receive(), second = await pair.Receive();
        Assert.That(first.Properties().Select(x => x.Name), Is.EquivalentTo(new[] { "type", "response_id", "input" }));
        Assert.That(first["input"]?[0]?["call_id"]?.ToString(), Is.EqualTo("call_1"));
        Assert.That(second["input"]?[0]?["output"]?.ToString(), Is.EqualTo("Saved two"));
        JObject completed = Lifecycle("response.completed", "resp_1", "completed");
        if (lane) completed["stream_id"] = "work";
        completed["response"]!["output"] = JArray.Parse("""[{"type":"function_call","id":"fc_1","call_id":"call_1","name":"lookup","arguments":"{}","agent":{"agent_name":"/root/reviewer"}}]""");
        await pair.Send(completed);
        await pair.Send(new() { ["type"] = "response.inject.failed", ["sequence_number"] = 8, ["response_id"] = "resp_1", ["input"] = second["input"]!.DeepClone(), ["error"] = new JObject { ["code"] = "response_already_completed", ["message"] = "Too late" } });
        await pair.Send(new() { ["type"] = "response.inject.created", ["sequence_number"] = 9, ["response_id"] = "resp_1" });
        Assert.That((await operation.WaitAsync(pair.Timeout.Token))?.Id, Is.EqualTo("resp_1"));
        Assert.That(accepted, Is.EqualTo(1));
        Assert.That(failed, Is.EqualTo(1));
        Assert.That(calls, Is.Zero);
        ResponseInjectionState state = pair.Connection.Injections.Single();
        Assert.That(state.PendingAcknowledgements, Is.Zero);
        Assert.That(state.AcceptedCount, Is.EqualTo(1));
        Assert.That(state.Failures.Single().Input[0]["output"]?.ToString(), Is.EqualTo("Saved two"));
        Assert.That(state.SubmittedInputs.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Disconnect_PreservesUnacknowledgedInjectionAsUnknown()
    {
        await using Pair pair = await Pair.Open(new() { Betas = [ResponseApiOptions.MultiAgentBeta] });
        Task<ResponseResult?> operation = pair.Connection.CreateResponseAsync(Request(), new()
        {
            OnResponseCreated = async e => await pair.Connection.InjectAsync(new(e.Response.Id, [new FunctionToolCallOutput("call_1", "Saved")]), pair.Timeout.Token)
        }, cancellationToken: pair.Timeout.Token);
        await pair.Receive();
        await pair.Send(Lifecycle("response.created", "resp_1", "in_progress"));
        await pair.Receive();
        await pair.Peer.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, pair.Timeout.Token);
        Assert.ThrowsAsync<EndOfStreamException>(async () => await operation.WaitAsync(pair.Timeout.Token));
        var state = pair.Connection.Injections.Single();
        Assert.That(state.UnknownAcknowledgements, Is.EqualTo(1));
        Assert.That(state.SubmittedInputs[0][0]["output"]?.ToString(), Is.EqualTo("Saved"));
    }

    [Test]
    public async Task MultiAgentRequiresBetaHandshake_AndInvalidInjectionDoesNotSend()
    {
        await using Pair pair = await Pair.Open();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await pair.Connection.SendResponseAsync(Request()));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await pair.Connection.InjectAsync(new("resp_1", [new FunctionToolCallOutput("call_1", "Saved")])));
        Assert.Throws<ArgumentException>(() => new ResponseInjectRequest("", [new FunctionToolCallOutput("call_1", "Saved")]).ToPayload());
        Assert.Throws<ArgumentException>(() => new ResponseInjectRequest("resp_1", []).ToPayload());
    }

    internal sealed class LocalHttp : IDisposable
    {
        internal readonly CancellationTokenSource Timeout = new(TimeSpan.FromSeconds(15));
        internal readonly HttpListener Listener = new();
        internal TornadoApi Api { get; }
        internal string? Beta;
        internal string Body = "";
        internal string? Path;
        internal LocalHttp()
        {
            using TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            string prefix = $"http://127.0.0.1:{port}/";
            Listener.Prefixes.Add(prefix);
            Listener.Start();
            Api = new TornadoApi(new Uri(prefix), "local-key", LLmProviders.OpenAi);
        }
        internal async Task Reply(string text, string contentType = "application/json", int status = 200)
        {
            HttpListenerContext context = await Listener.GetContextAsync().WaitAsync(Timeout.Token);
            Beta = context.Request.Headers["OpenAI-Beta"];
            Path = context.Request.Url!.AbsolutePath;
            Body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, Timeout.Token);
            context.Response.Close();
        }
        public void Dispose() { Listener.Close(); Timeout.Dispose(); }
    }
}
