using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests.Responses;

[TestFixture]
public class ResponsesOctoberContractTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void AsyncToolDefinitions_RoundTripFunctionAndCustomFlags(bool async)
    {
        ResponseRequest request = new()
        {
            Model = "gpt-6-astra", InputString = "Look this up",
            Tools = [new ResponseFunctionTool { Name = "lookup", Async = async, DeferLoading = true, Parameters = new JObject() },
                new ResponseCustomTool { Name = "query", Async = async, DeferLoading = true }]
        };
        var provider = new TornadoApi(LLmProviders.OpenAi, "test-key").GetProvider(LLmProviders.OpenAi);
        JObject json = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        foreach (JToken tool in json["tools"]!)
        {
            Assert.That(tool["async"]?.Value<bool>(), Is.EqualTo(async));
            Assert.That(tool["defer_loading"]?.Value<bool>(), Is.True);
            ResponseTool parsed = tool.ToObject<ResponseTool>()!;
            Assert.That(parsed is ResponseFunctionTool f ? f.Async : ((ResponseCustomTool)parsed).Async, Is.EqualTo(async));
        }
        request.Tools = [new ResponseFunctionTool { Name = "synchronous" }];
        json = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        Assert.That(json["tools"]?[0]?["async"], Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AsyncToolCalls_PreserveIdsAndFlagsThroughOutputToInputReplay(bool async)
    {
        ResponseResult response = JsonConvert.DeserializeObject<ResponseResult>($$"""
            {"id":"resp_async","output":[
              {"type":"function_call","id":"fc_1","call_id":"call_function","name":"lookup","arguments":"{}","async":{{async.ToString().ToLowerInvariant()}}},
              {"type":"custom_tool_call","id":"ct_1","call_id":"call_custom","name":"query","input":"select 1","async":{{async.ToString().ToLowerInvariant()}}}
            ]}
            """)!;
        var function = (ResponseFunctionToolCallItem)response.Output![0];
        var custom = (ResponseCustomToolCallItem)response.Output[1];
        Assert.That(function.Async, Is.EqualTo(async));
        Assert.That(custom.Async, Is.EqualTo(async));
        List<ResponseInputItem> replay = response.Output.ToInputItems();
        Assert.That(((FunctionToolCallInput)replay[0]).Async, Is.EqualTo(async));
        Assert.That(((CustomToolCallInput)replay[1]).Async, Is.EqualTo(async));
        JArray json = JArray.FromObject(replay);
        Assert.That(json[0]["call_id"]?.ToString(), Is.EqualTo("call_function"));
        Assert.That(json[1]["call_id"]?.ToString(), Is.EqualTo("call_custom"));
        Assert.That(json[1]["input"]?.ToString(), Is.EqualTo("select 1"));
        Assert.That(json[0]["async"]?.Value<bool>(), Is.EqualTo(async));
        Assert.That(json[1]["async"]?.Value<bool>(), Is.EqualTo(async));
    }

    [TestCase(true, null)]
    [TestCase(null, true)]
    [TestCase(false, true)]
    public async Task AsyncDelegates_AreLeftForTheApplicationToSchedule(bool? definitionAsync, bool? callAsync)
    {
        bool invoked = false;
        ResponseRequest request = new()
        {
            Tools = [new ResponseFunctionTool
            {
                Name = "lookup", Async = definitionAsync,
                Delegate = (Action)(() => invoked = true)
            }]
        };
        ResponseResult result = new()
        {
            Output = [new ResponseFunctionToolCallItem { CallId = "call_pending", Name = "lookup", Arguments = "{}", Async = callAsync }]
        };
        await ResponsesEndpoint.HandleResponse(request, result);
        Assert.That(invoked, Is.False);
        Assert.That(((ResponseFunctionToolCallItem)result.Output[0]).Result, Is.Null);
    }

    [Test]
    public void ConfigurationUpdates_RoundTripAndKeepRequestLevelEffortUnchanged()
    {
        ResponseRequest request = new()
        {
            Model = "gpt-6-astra", PreviousResponseId = "resp_initial",
            Reasoning = new ReasoningConfiguration(ResponseReasoningEfforts.Low),
            InputItems = [new ResponseConfigurationUpdate(ResponseReasoningEfforts.High), new ResponseInputMessage(ChatMessageRoles.User, "Review failure modes")]
        };
        var provider = new TornadoApi(LLmProviders.OpenAi, "test-key").GetProvider(LLmProviders.OpenAi);
        JObject json = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        Assert.That(json["reasoning"]?["effort"]?.ToString(), Is.EqualTo("low"));
        Assert.That(json["input"]?[0]?["type"]?.ToString(), Is.EqualTo("configuration_update"));
        Assert.That(json["input"]?[0]?["reasoning"]?["effort"]?.ToString(), Is.EqualTo("high"));
        Assert.That(json["input"]?[0]?["reasoning"]?["summary"], Is.Null);
        var parsed = json["input"]![0]!.ToObject<ResponseInputItem>();
        Assert.That(parsed, Is.TypeOf<ResponseConfigurationUpdate>());
        Assert.That(((ResponseConfigurationUpdate)parsed!).Reasoning.Effort, Is.EqualTo(ResponseReasoningEfforts.High));
    }

    [TestCase("cache_hit")]
    [TestCase("comparison_response_not_found")]
    [TestCase("unavailable")]
    public void CacheDiagnostics_AllowOutcomesWithoutMissTokenCounts(string type)
    {
        var result = JsonConvert.DeserializeObject<ResponseResult>($$$"""{"prompt_cache_diagnostics":{"type":"{{{type}}}"}}""")!;
        Assert.That(result.PromptCacheDiagnostics!.Type, Is.EqualTo(type));
        Assert.That(result.PromptCacheDiagnostics.CacheMissedTokens, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PublicResponses_CacheComparisonAndDiagnosticResultsWorkWithJsonAndSse(bool streaming)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        string prefix = $"http://127.0.0.1:{port}/";
        using HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();
        TornadoApi api = new(new Uri(prefix), "test-key", LLmProviders.OpenAi);
        ResponseRequest initial = new()
        {
            Model = "gpt-6-astra", InputString = "Hello",
            PromptCacheOptions = new() { ComparisonResponseId = "resp_baseline", Ttl = ResponsePromptCacheTtl.TwelveHours }
        };
        ResponseRequest request = new(initial);
        Assert.That(request.PromptCacheOptions, Is.Not.SameAs(initial.PromptCacheOptions));
        ResponseResult? result = null;
        Task operation = Run();
        HttpListenerContext context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using StreamReader reader = new(context.Request.InputStream);
        JObject body = JObject.Parse(await reader.ReadToEndAsync(timeout.Token));
        Assert.That(context.Request.Url!.AbsolutePath, Does.EndWith("/responses"));
        Assert.That(body["prompt_cache_options"]?["comparison_response_id"]?.ToString(), Is.EqualTo("resp_baseline"));
        Assert.That(body["prompt_cache_options"]?["ttl"]?.ToString(), Is.EqualTo("12h"));
        string json = """{"id":"resp_current","status":"completed","output":[],"prompt_cache_diagnostics":{"type":"cache_miss","reason":"tools_changed","comparison_reusable_tokens":5629,"cache_missed_tokens":5629},"usage":{"input_tokens":6000,"input_tokens_details":{"cached_tokens":0,"cache_write_tokens":5629}}}""";
        string payload = streaming ? "event: response.completed\ndata: {\"type\":\"response.completed\",\"sequence_number\":1,\"response\":" + json + "}\n\n" : json;
        context.Response.ContentType = streaming ? "text/event-stream" : "application/json";
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        await context.Response.OutputStream.WriteAsync(bytes, timeout.Token);
        context.Response.Close();
        await operation.WaitAsync(timeout.Token);
        Assert.That(result!.PromptCacheDiagnostics!.Type, Is.EqualTo("cache_miss"));
        Assert.That(result.PromptCacheDiagnostics.Reason, Is.EqualTo("tools_changed"));
        Assert.That(result.PromptCacheDiagnostics.ComparisonReusableTokens, Is.EqualTo(5629));
        Assert.That(result.PromptCacheDiagnostics.CacheMissedTokens, Is.EqualTo(5629));

        async Task Run()
        {
            if (streaming)
                await api.Responses.StreamResponseRich(request, new() { OnResponseCompleted = evt => { result = evt.Response; return ValueTask.CompletedTask; } }, timeout.Token);
            else
                result = await api.Responses.CreateResponse(request);
        }
    }
}
