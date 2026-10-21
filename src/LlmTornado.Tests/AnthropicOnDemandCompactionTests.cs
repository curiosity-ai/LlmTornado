using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Compaction;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicOnDemandCompactionTests
{
    private const string Response = """
        {"id":"msg-summary","type":"message","role":"assistant","model":"claude-opus-5-5",
        "content":[{"type":"compaction","content":"Earlier decisions and tasks","signature":"opaque-signature"}],
        "stop_reason":"compaction","usage":{"input_tokens":0,"output_tokens":0,
        "iterations":[{"type":"compaction","input_tokens":144,"output_tokens":276}]}}
        """;

    [Test]
    public void OnDemandRequest_SendsSummarizeWithoutThresholdAndUsesNewBeta()
    {
        CompactionRequest request = new(ChatModel.Anthropic.Claude5.Opus55, "Summarize this conversation") { OnDemand = true };
        var provider = new TornadoApi(LLmProviders.Anthropic, "test-key").GetProvider(LLmProviders.Anthropic);
        JObject body = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        Assert.That(body["compaction"]?["type"]?.ToString(), Is.EqualTo("summarize"));
        Assert.That(body["context_management"], Is.Null);
        using HttpRequestMessage message = provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, false, request);
        Assert.That(string.Join(",", message.Headers.GetValues("anthropic-beta")), Does.Contain(CompactionRequest.OnDemandBetaHeader));
        Assert.That(string.Join(",", message.Headers.GetValues("anthropic-beta")), Does.Not.Contain(CompactionRequest.BetaHeader));
    }

    [Test]
    public void SummaryResponse_ReplaysSignatureAndAutomaticallySelectsBeta()
    {
        var provider = new TornadoApi(LLmProviders.Anthropic, "test-key").GetProvider(LLmProviders.Anthropic);
        CompactionResult result = provider.InboundMessage<CompactionResult>(Response, null, null)!;
        Assert.That(result.WasCompacted, Is.True);
        Assert.That(result.Model, Is.EqualTo("claude-opus-5-5"));
        Assert.That(result.Signature, Is.EqualTo("opaque-signature"));
        Assert.That(result.CompactionContent, Is.EqualTo("Earlier decisions and tasks"));
        ChatRequest next = new()
        {
            Model = ChatModel.Anthropic.Claude5.Opus55,
            Messages = [result.AssistantMessage!, new ChatMessage(ChatMessageRoles.User, "Continue")]
        };
        JObject body = JObject.Parse(next.Serialize(provider).Body.ToString()!);
        Assert.That(body["messages"]![0]!["content"]![0]!["signature"]!.ToString(), Is.EqualTo("opaque-signature"));
        Assert.That(body["messages"]![0]!["content"]![0]!["content"]!.ToString(), Is.EqualTo("Earlier decisions and tasks"));
        using HttpRequestMessage message = provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, false, next);
        Assert.That(string.Join(",", message.Headers.GetValues("anthropic-beta")), Does.Contain(CompactionRequest.OnDemandBetaHeader));
    }

    [TestCase("claude-opus-5-5", false)]
    [TestCase("claude-opus-5", true)]
    [TestCase("claude-opus-4-8", true)]
    public void FastMode_UsesBetaOnlyForPreviewModels(string model, bool requiresBeta)
    {
        var provider = new TornadoApi(LLmProviders.Anthropic, "test-key").GetProvider(LLmProviders.Anthropic);
        ChatRequest request = new() { Model = model, Speed = ChatRequestSpeeds.Fast };
        using HttpRequestMessage message = provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, false, request);
        string headers = string.Join(",", message.Headers.GetValues("anthropic-beta"));
        Assert.That(headers.Contains("fast-mode-2026-02-01"), Is.EqualTo(requiresBeta));
    }

    [Test]
    public void OnDemandAndThresholdSettings_RejectCombination()
    {
        var provider = new TornadoApi(LLmProviders.Anthropic, "test-key").GetProvider(LLmProviders.Anthropic);
        ChatRequest request = new()
        {
            Model = ChatModel.Anthropic.Claude5.Opus55,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Compaction = new(), ContextManagement = AnthropicContextManagement.Default()
            })
        };
        Assert.Throws<ArgumentException>(() => request.Serialize(provider));
    }

    [Test]
    public async Task PublicConversationStream_PreservesSignedSummaryWhitespace()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using HttpListener listener = new();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        Task<HttpListenerContext> incoming = listener.GetContextAsync();
        TornadoApi api = new(new AnthropicEndpointProvider
        {
            Auth = new ProviderAuthentication(LLmProviders.Anthropic, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{port}/v1/{{0}}{{1}}"
        });
        Conversation conversation = api.Chat.CreateConversation(ChatModel.Anthropic.Claude5.Opus55);
        conversation.RequestParameters.TrimResponseStart = true;
        conversation.RequestParameters.VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions { Compaction = new() });
        conversation.AppendUserInput("Summarize");
        ChatMessagePart? received = null;
        Task operation = conversation.StreamResponseRich(new ChatStreamEventHandler
        {
            MessagePartHandler = part => { received = part; return ValueTask.CompletedTask; }
        }, timeout.Token);
        HttpListenerContext context = await incoming.WaitAsync(timeout.Token);
        Assert.That(context.Request.Headers["anthropic-beta"], Does.Contain(CompactionRequest.OnDemandBetaHeader));
        using StreamReader requestReader = new(context.Request.InputStream);
        JObject requestBody = JObject.Parse(await requestReader.ReadToEndAsync(timeout.Token));
        Assert.That(requestBody["compaction"]?["type"]?.ToString(), Is.EqualTo("summarize"));
        context.Response.ContentType = "text/event-stream";
        byte[] bytes = Encoding.UTF8.GetBytes("""
            event: message_start
            data: {"type":"message_start","message":{"id":"msg-1","type":"message","role":"assistant","content":[],"model":"claude-opus-5-5","usage":{"input_tokens":0,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"compaction","content":"  Signed summary","signature":"signed-by-server"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"compaction"},"usage":{"output_tokens":0}}

            event: message_stop
            data: {"type":"message_stop"}

            """);
        await context.Response.OutputStream.WriteAsync(bytes, timeout.Token);
        context.Response.Close();
        await operation.WaitAsync(timeout.Token);
        Assert.That(received!.Text, Is.EqualTo("  Signed summary"));
        Assert.That(received.CompactionSignature, Is.EqualTo("signed-by-server"));
    }

    [Test]
    public async Task StreamingSummary_DeliversWholeSignedBlockWithoutDeltas()
    {
        const string sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg-summary","type":"message","role":"assistant","content":[],"model":"claude-opus-5-5","usage":{"input_tokens":0,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"compaction","content":"Summary in full","signature":"signed-stream"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"compaction"},"usage":{"output_tokens":0,"iterations":[{"type":"compaction","input_tokens":144,"output_tokens":276}]}}

            event: message_stop
            data: {"type":"message_stop"}

            """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(sse));
        using StreamReader reader = new(stream);
        List<ChatResult?> chunks = [];
        await foreach (var chunk in new AnthropicEndpointProvider().InboundStream(reader, new ChatRequest { Model = ChatModel.Anthropic.Claude5.Opus55 }, null)) chunks.Add(chunk);
        var summaries = chunks.SelectMany(c => c?.Choices ?? []).SelectMany(c => c.Delta?.Parts ?? []).Where(p => p.Type == ChatMessageTypes.Compaction).ToList();
        Assert.That(summaries, Has.Count.EqualTo(1));
        Assert.That(summaries[0].Text, Is.EqualTo("Summary in full"));
        Assert.That(summaries[0].CompactionSignature, Is.EqualTo("signed-stream"));
    }
}
