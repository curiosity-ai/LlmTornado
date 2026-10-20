using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicParallelToolCallsTests
{
    private const string ParallelToolUseSse = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_parallel","type":"message","role":"assistant","content":[],"model":"claude-sonnet-4-6","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":400,"output_tokens":2}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_paris","name":"get_weather","input":{}}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"city\": \"Paris\"}"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: content_block_start
        data: {"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_london","name":"get_weather","input":{}}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"city\": \"London\"}"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":1}

        event: content_block_start
        data: {"type":"content_block_start","index":2,"content_block":{"type":"tool_use","id":"toolu_tokyo","name":"get_weather","input":{}}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":2,"delta":{"type":"input_json_delta","partial_json":"{\"city\": \"Tokyo\"}"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":2}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":90}}

        event: message_stop
        data: {"type":"message_stop"}

        """;

    private const string FinalAnswerJson = """
        {"id":"msg_final","type":"message","role":"assistant","model":"claude-sonnet-4-6","stop_reason":"end_turn","stop_sequence":null,
         "content":[{"type":"text","text":"All three are cloudy."}],
         "usage":{"input_tokens":600,"output_tokens":8}}
        """;

    private const string ThinkingAnswerJson = """
        {"id":"msg_thinking","type":"message","role":"assistant","model":"claude-sonnet-4-6","stop_reason":"end_turn","stop_sequence":null,
         "content":[{"type":"thinking","thinking":"Paris, London and Tokyo all report clouds.","signature":"sig_abc"},{"type":"text","text":"All three are cloudy."}],
         "usage":{"input_tokens":600,"output_tokens":30}}
        """;

    private const string TextThenToolUseJson = """
        {"id":"msg_mixed","type":"message","role":"assistant","model":"claude-sonnet-4-6","stop_reason":"tool_use","stop_sequence":null,
         "content":[{"type":"text","text":"Let me check."},{"type":"tool_use","id":"toolu_paris","name":"get_weather","input":{"city":"Paris"}}],
         "usage":{"input_tokens":600,"output_tokens":20}}
        """;

    private const string TwoTextBlocksJson = """
        {"id":"msg_two","type":"message","role":"assistant","model":"claude-sonnet-4-6","stop_reason":"end_turn","stop_sequence":null,
         "content":[{"type":"text","text":"Paris is cloudy."},{"type":"text","text":"London is cloudy."}],
         "usage":{"input_tokens":600,"output_tokens":12}}
        """;

    private static readonly string[] ToolUseIds = ["toolu_paris", "toolu_london", "toolu_tokyo"];
    private static readonly string[] Cities = ["Paris", "London", "Tokyo"];

    [Test]
    public async Task InboundStream_ParallelToolUse_YieldsToolCallsOnce()
    {
        AnthropicEndpointProvider provider = new AnthropicEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(ParallelToolUseSse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new LlmTornado.Chat.ChatMessage(ChatMessageRoles.User, "test")]
        };

        List<ChatResult?> toolChunks = [];
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk?.Choices?.FirstOrDefault()?.Delta?.ToolCalls?.Count > 0)
            {
                toolChunks.Add(chunk);
            }
        }

        Assert.That(toolChunks, Has.Count.EqualTo(1));
        List<ToolCall> calls = toolChunks[0]!.Choices![0].Delta!.ToolCalls!;
        Assert.That(calls.Select(x => x.Id), Is.EqualTo(ToolUseIds));
        Assert.That(calls.Select(x => x.FunctionCall!.Arguments), Is.EqualTo(new[] { "{\"city\": \"Paris\"}", "{\"city\": \"London\"}", "{\"city\": \"Tokyo\"}" }));
        Assert.That(toolChunks[0]!.Usage?.CompletionTokens, Is.EqualTo(90));
    }

    [Test]
    public async Task ChatClient_Streaming_ParallelToolCalls_EmitsEachCallOnce()
    {
        using FakeAnthropicServer server = new FakeAnthropicServer("text/event-stream", ParallelToolUseSse);
        IChatClient client = CreateClient(server.Port);

        List<FunctionCallContent> calls = [];
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(Prompt(), Options()))
        {
            calls.AddRange(update.Contents.OfType<FunctionCallContent>());
        }

        Assert.That(calls.Select(x => x.CallId), Is.EqualTo(ToolUseIds));
        Assert.That(calls.Select(x => x.Arguments?["city"]?.ToString()), Is.EqualTo(new[] { "Paris", "London", "Tokyo" }));
    }

    [Test]
    public async Task ChatClient_ToolMessageWithSeveralResults_SendsEveryToolResult()
    {
        using FakeAnthropicServer server = new FakeAnthropicServer("application/json", FinalAnswerJson);
        IChatClient client = CreateClient(server.Port);

        // the shape UseFunctionInvocation() sends back after a parallel round: one tool message holding every result
        List<global::Microsoft.Extensions.AI.ChatMessage> history = Prompt();
        history.Add(new global::Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant,
            ToolUseIds.Select((id, i) => (AIContent)new FunctionCallContent(id, "get_weather", new Dictionary<string, object?> { ["city"] = Cities[i] })).ToList()));
        history.Add(new global::Microsoft.Extensions.AI.ChatMessage(ChatRole.Tool,
            ToolUseIds.Select((id, i) => (AIContent)new FunctionResultContent(id, $"{Cities[i]}: 18 C, cloudy")).ToList()));

        await client.GetResponseAsync(history, Options());

        Assert.That(server.RequestBodies, Has.Count.EqualTo(1));

        JArray messages = (JArray)JObject.Parse(server.RequestBodies[0])["messages"]!;
        List<JToken> toolResults = messages
            .SelectMany(m => m["content"] as JArray ?? [])
            .Where(b => b["type"]?.Value<string>() == "tool_result")
            .ToList();

        Assert.That(toolResults.Select(b => b["tool_use_id"]?.Value<string>()), Is.EqualTo(ToolUseIds));
        Assert.That(toolResults.Select(b => b["content"]?.Value<string>()), Is.EqualTo(Cities.Select(c => $"{c}: 18 C, cloudy")));
    }

    [Test]
    public async Task ChatClient_Streaming_ReportsUsageOnce()
    {
        using FakeAnthropicServer server = new FakeAnthropicServer("text/event-stream", ParallelToolUseSse);
        IChatClient client = CreateClient(server.Port);

        List<ChatResponseUpdate> updates = [];
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(Prompt(), Options()))
        {
            updates.Add(update);
        }

        UsageDetails usage = updates.SelectMany(x => x.Contents).OfType<UsageContent>().Single().Details;
        Assert.That(usage.InputTokenCount, Is.EqualTo(400));
        Assert.That(usage.OutputTokenCount, Is.EqualTo(90));
    }

    [Test]
    public async Task ChatClient_TextReply_ReturnsTextOnce()
    {
        using FakeAnthropicServer server = new FakeAnthropicServer("application/json", FinalAnswerJson);
        IChatClient client = CreateClient(server.Port);

        ChatResponse response = await client.GetResponseAsync(Prompt());

        Assert.That(response.Text, Is.EqualTo("All three are cloudy."));
        Assert.That(response.Messages.Single().Contents.OfType<TextContent>().Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ChatClient_ThinkingReply_ReturnsReasoningOnce()
    {
        using FakeAnthropicServer server = new FakeAnthropicServer("application/json", ThinkingAnswerJson);
        IChatClient client = CreateClient(server.Port);

        ChatResponse response = await client.GetResponseAsync(Prompt());

        List<AIContent> contents = response.Messages.Single().Contents.ToList();
        Assert.That(contents.OfType<TextReasoningContent>().Select(x => x.Text), Is.EqualTo(new[] { "Paris, London and Tokyo all report clouds." }));
        Assert.That(response.Text, Is.EqualTo("All three are cloudy."));
    }

    [Test]
    public async Task ChatClient_TextThenToolUse_ReturnsTextAndToolCall()
    {
        using FakeAnthropicServer server = new FakeAnthropicServer("application/json", TextThenToolUseJson);
        IChatClient client = CreateClient(server.Port);

        ChatResponse response = await client.GetResponseAsync(Prompt(), Options());

        List<AIContent> contents = response.Messages.Single().Contents.ToList();
        Assert.That(contents.OfType<TextContent>().Select(x => x.Text), Is.EqualTo(new[] { "Let me check." }));
        FunctionCallContent call = contents.OfType<FunctionCallContent>().Single();
        Assert.That(call.CallId, Is.EqualTo("toolu_paris"));
        Assert.That(call.Name, Is.EqualTo("get_weather"));
        Assert.That(response.FinishReason, Is.EqualTo(ChatFinishReason.ToolCalls));
    }

    [Test]
    public async Task ChatClient_TwoTextBlocks_ReturnsBothBlocks()
    {
        using FakeAnthropicServer server = new FakeAnthropicServer("application/json", TwoTextBlocksJson);
        IChatClient client = CreateClient(server.Port);

        ChatResponse response = await client.GetResponseAsync(Prompt());

        Assert.That(response.Messages.Single().Contents.OfType<TextContent>().Select(x => x.Text),
            Is.EqualTo(new[] { "Paris is cloudy.", "London is cloudy." }));
    }

    private static IChatClient CreateClient(int port)
    {
        TornadoApi api = new TornadoApi(new AnthropicEndpointProvider
        {
            Auth = new ProviderAuthentication(LLmProviders.Anthropic, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{port}/v1/{{0}}{{1}}"
        });

        return api.AsChatClient(ChatModel.Anthropic.Claude46.Sonnet);
    }

    private static ChatOptions Options() => new ChatOptions
    {
        Tools = [AIFunctionFactory.Create((string city) => $"{city}: 18 C, cloudy", "get_weather", "Gets the current weather for a city.")]
    };

    private static List<global::Microsoft.Extensions.AI.ChatMessage> Prompt() =>
    [
        new global::Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "Get the weather for Paris, London and Tokyo in parallel.")
    ];

    private sealed class FakeAnthropicServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string contentType;
        private readonly Queue<string> responses;

        public int Port { get; }
        public List<string> RequestBodies { get; } = [];

        public FakeAnthropicServer(string contentType, params string[] responses)
        {
            this.contentType = contentType;
            this.responses = new Queue<string>(responses);

            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            Port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            listener.Start();
            _ = Serve();
        }

        private async Task Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // listener closed
                    return;
                }

                using (StreamReader reader = new StreamReader(context.Request.InputStream))
                {
                    RequestBodies.Add(await reader.ReadToEndAsync().ConfigureAwait(false));
                }

                string body = responses.Count > 0 ? responses.Dequeue() : """{"type":"error","error":{"type":"invalid_request_error","message":"unexpected request"}}""";
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = 200;
                context.Response.ContentType = contentType;
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            listener.Close();
        }
    }
}
