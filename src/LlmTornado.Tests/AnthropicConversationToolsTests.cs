using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicConversationToolsTests
{
    private static IEndpointProvider Provider => new TornadoApi(LLmProviders.Anthropic, "test-key").GetProvider(LLmProviders.Anthropic);
    private static Tool Function() => new(new ToolFunction("lookup", "Look up a record", JObject.Parse("""{"type":"object","properties":{"key":{"type":"string"}},"required":["key"]}"""))) { Strict = true, DeferLoading = true };
    private static ChatRequest Request(params ChatMessagePart[] parts) => new()
    {
        Model = ChatModel.Anthropic.Claude5.Opus55,
        Messages = [new(ChatMessageRoles.User, "Look this up"), new(ChatMessageRoles.System, parts.ToList())]
    };
    private static JObject Body(ChatRequest request) => JObject.Parse(request.Serialize(Provider).Body.ToString()!);
    private static string Headers(ChatRequest request)
    {
        using HttpRequestMessage message = Provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, false, request);
        return string.Join(",", message.Headers.GetValues("anthropic-beta"));
    }

    [Test]
    public void InlineFunctionDefinition_PreservesSchemaAndMixedSystemBlockOrdering()
    {
        ChatRequest request = Request(new ChatMessagePart("A tool is now available."), new(AnthropicToolChange.Add(AnthropicInlineTool.Define(Function()))), new("Use it when needed."));
        JObject body = Body(request);
        JToken content = body["messages"]![1]!["content"]!;
        Assert.That(content[0]?["text"]?.ToString(), Is.EqualTo("A tool is now available."));
        Assert.That(content[1]?["type"]?.ToString(), Is.EqualTo("tool_addition"));
        Assert.That(content[1]?["tool"]?["type"]?.ToString(), Is.EqualTo("tool_definition"));
        var definition = content[1]!["tool"]!["definition"]!;
        Assert.That(definition["name"]?.ToString(), Is.EqualTo("lookup"));
        Assert.That(definition["input_schema"]?["required"]?.Values<string>(), Is.EqualTo(new[] { "key" }));
        Assert.That(definition["strict"]?.Value<bool>(), Is.True);
        Assert.That(definition["defer_loading"]?.Value<bool>(), Is.True);
        Assert.That(content[2]?["text"]?.ToString(), Is.EqualTo("Use it when needed."));
        Assert.That(Headers(request), Does.Contain("inline-tools-2026-09-15").And.Not.Contain("mid-conversation-tool-changes-2026-07-01"));
    }

    [Test]
    public void InlineServerToolDefinition_UsesNativeVersionAndConfiguration()
    {
        var native = new VendorAnthropicChatRequestBuiltInToolWebSearch20250305 { MaxUses = 4 };
        JObject body = Body(Request(new ChatMessagePart(AnthropicToolChange.Add(AnthropicInlineTool.Define(native)))));
        JToken definition = body["messages"]![1]!["content"]![0]!["tool"]!["definition"]!;
        Assert.That(definition["type"]?.ToString(), Is.EqualTo("web_search_20250305"));
        Assert.That(definition["max_uses"]?.Value<int>(), Is.EqualTo(4));
    }

    [TestCase("tool_reference")]
    [TestCase("mcp_tool_reference")]
    [TestCase("mcp_toolset_reference")]
    public void ToolReferences_SupportAddRemoveAndPersistence(string type)
    {
        AnthropicInlineTool reference = type switch
        {
            "tool_reference" => AnthropicInlineTool.Reference("lookup"),
            "mcp_tool_reference" => AnthropicInlineTool.McpTool("calendar", "list_events"),
            _ => AnthropicInlineTool.McpToolset("calendar")
        };
        var removal = AnthropicToolChange.Remove(reference);
        var restored = JsonConvert.DeserializeObject<AnthropicToolChange>(JsonConvert.SerializeObject(removal))!;
        Assert.That(restored.IsRemoval, Is.True);
        JObject body = Body(Request(new ChatMessagePart(AnthropicToolChange.Add(reference)), new(restored)));
        var blocks = body["messages"]![1]!["content"]!;
        Assert.That(blocks[0]?["type"]?.ToString(), Is.EqualTo("tool_addition"));
        Assert.That(blocks[1]?["type"]?.ToString(), Is.EqualTo("tool_removal"));
        Assert.That(blocks[1]?["tool"]?["type"]?.ToString(), Is.EqualTo(type));
    }

    [Test]
    public void InlineMcpToolset_IsNotAutomaticallyExposedBeforeItsAddition()
    {
        ChatRequest request = Request(new ChatMessagePart(AnthropicToolChange.Add(AnthropicInlineTool.Define(AnthropicMcpToolset.ForServer("calendar")))));
        request.Tools = [Function()];
        request.VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
        {
            McpServers = [new AnthropicMcpServer { Name = "calendar", Url = "https://mcp.example.com/calendar", AuthorizationToken = "server-token" }]
        });
        JObject body = Body(request);
        Assert.That(body["tools"]!.Any(t => t["type"]?.ToString() == "mcp_toolset"), Is.False);
        Assert.That(body["mcp_servers"]?[0]?["authorization_token"]?.ToString(), Is.EqualTo("server-token"));
        Assert.That(body["messages"]![1]!["content"]![0]!["tool"]!["definition"]!["mcp_server_name"]?.ToString(), Is.EqualTo("calendar"));
        string headers = Headers(request);
        Assert.That(headers, Does.Contain("inline-tools-2026-09-15").And.Contain(AnthropicMcpBetaHeaders.McpClientSeptember2026));
        Assert.That(headers, Does.Not.Contain(AnthropicMcpBetaHeaders.McpClient));
    }

    [Test]
    public void PinnedMcpToolsets_PreserveAnExplicitlyEmptyListAndUseNewBeta()
    {
        ChatRequest request = Request(new ChatMessagePart("Use only the pinned tools"));
        request.VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
        {
            McpServers = [new() { Name = "calendar", Url = "https://mcp.example.com" }],
            McpToolsets = [new() { McpServerName = "calendar", Tools = [] }]
        });
        var body = Body(request);
        Assert.That(body["tools"]?[0]?["tools"], Is.TypeOf<JArray>());
        Assert.That(body["tools"]?[0]?["tools"]?.Count(), Is.Zero);
        Assert.That(Headers(request), Does.Contain(AnthropicMcpBetaHeaders.McpClientSeptember2026));
    }

    [Test]
    public void FetchedMcpListings_AreAttachedBeforeTextAndReplayWithoutUnknownPlaceholders()
    {
        ChatResult result = Provider.InboundMessage<ChatResult>(Response, null, null)!;
        ChatMessage message = result.Choices.Single().Message!;
        Assert.That(message.Parts, Has.Count.EqualTo(2));
        Assert.That(message.Parts![0].Type, Is.EqualTo(ChatMessageTypes.AnthropicMcpToolListing));
        Assert.That(message.Parts[1].Text, Is.EqualTo("Found two events."));
        AnthropicMcpToolListing listing = message.Parts[0].AnthropicMcpToolListing!;
        Assert.That(listing.Tools[0].InputSchema["type"]?.ToString(), Is.EqualTo("object"));
        ChatRequest request = new() { Model = ChatModel.Anthropic.Claude5.Opus55, Messages = [new(ChatMessageRoles.User, "What's scheduled?"), message] };
        var body = Body(request);
        Assert.That(body["messages"]?[1]?["content"]?[0]?["type"]?.ToString(), Is.EqualTo("mcp_tool_listing"));
        Assert.That(body["messages"]?[1]?["content"]?[0]?["tools"]?[0]?["name"]?.ToString(), Is.EqualTo("list_events"));
        Assert.That(Headers(request), Does.Contain(AnthropicMcpBetaHeaders.McpClientSeptember2026));
    }

    [Test]
    public void ThinkingTransformations_PreserveKnownAndFutureValuesWithoutEnablingEnforcement()
    {
        ChatResult result = Provider.InboundMessage<ChatResult>(Response, null, null)!;
        var transformations = result.VendorExtensions!.Anthropic!.InputTransformations!;
        Assert.That(transformations.Select(x => x.Type), Is.EqualTo(new[] { "thinking_dropped", "thinking_mismatch_allowed", "future_check" }));
        Assert.That(transformations[0].Path, Is.EqualTo("messages.1.content.0"));
        Assert.That(transformations[2].Reason, Is.EqualTo("future_reason"));
        ChatRequest request = Request(new ChatMessagePart("Continue"));
        request.VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions { EnableThinkingBindingDiagnostics = true });
        Assert.That(Headers(request), Does.Contain("thinking-binding-controls-2026-08-01"));
        Assert.That(Body(request)["thinking"]?["block_binding"], Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StreamTransformations_UseServingModelsFinalEntriesAndPreserveEmptyArrays(bool root)
    {
        string delta = root
            ? """{"type":"message_delta","delta":{"stop_reason":"end_turn"},"input_transformations":[],"usage":{"output_tokens":2}}"""
            : """{"type":"message_delta","delta":{"stop_reason":"end_turn","input_transformations":[{"type":"thinking_dropped","path":"messages.1.content.0","reason":"model_binding_mismatch"}]},"usage":{"output_tokens":2}}""";
        string sse = "event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"model\":\"claude-opus-5-5\",\"content\":[],\"usage\":{\"input_tokens\":5,\"output_tokens\":0},\"input_transformations\":[{\"type\":\"thinking_mismatch_allowed\",\"path\":\"messages.1.content.0\",\"reason\":\"prefix_binding_mismatch\"}]}}\n\nevent: message_delta\ndata: " + delta + "\n\nevent: message_stop\ndata: {\"type\":\"message_stop\"}\n\n";
        using StreamReader reader = new(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatResult? last = null;
        await foreach (var result in new AnthropicEndpointProvider().InboundStream(reader, new ChatRequest { Model = ChatModel.Anthropic.Claude5.Opus55 }, null)) last = result;
        var final = last!.VendorExtensions!.Anthropic!.InputTransformations!;
        Assert.That(final, Has.Count.EqualTo(root ? 0 : 1));
        if (!root) Assert.That(final[0].Reason, Is.EqualTo("model_binding_mismatch"));
    }

    [Test]
    public void InvalidToolChanges_RejectRemovalDefinitionsAndNonSystemPlacement()
    {
        Assert.Throws<ArgumentException>(() => Body(Request(new ChatMessagePart(AnthropicToolChange.Remove(AnthropicInlineTool.Define(Function()))))));
        var request = Request(new ChatMessagePart(AnthropicToolChange.Add(AnthropicInlineTool.Reference("lookup"))));
        request.Messages![1].Role = ChatMessageRoles.User;
        Assert.Throws<ArgumentException>(() => Body(request));
        request.Messages.RemoveAt(0);
        request.Messages[0].Role = ChatMessageRoles.System;
        Assert.Throws<ArgumentException>(() => Body(request));
    }

    [Test]
    public async Task ConversationStream_PreservesWholeMcpListingInHistoryAndSelectsNewBeta()
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
        TornadoApi api = new(new AnthropicEndpointProvider
        {
            Auth = new(LLmProviders.Anthropic, "test-key"), UrlResolver = (_, _, _) => prefix + "v1/{0}{1}"
        });
        var conversation = api.Chat.CreateConversation(ChatModel.Anthropic.Claude5.Opus55);
        conversation.RequestParameters.VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions { EnableMcpToolListing = true });
        conversation.AppendUserInput("What's scheduled?");
        List<ChatMessagePart> delivered = [];
        Task operation = conversation.StreamResponseRich(new ChatStreamEventHandler
        {
            MessagePartHandler = part => { delivered.Add(part); return ValueTask.CompletedTask; }
        }, timeout.Token);
        HttpListenerContext context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        Assert.That(context.Request.Headers["anthropic-beta"], Does.Contain(AnthropicMcpBetaHeaders.McpClientSeptember2026));
        var listing = JObject.Parse(Response)["content"]![0]!;
        string sse = "event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_stream\",\"model\":\"claude-opus-5-5\",\"content\":[],\"usage\":{\"input_tokens\":5,\"output_tokens\":0}}}\n\nevent: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":" + listing.ToString(Formatting.None) + "}\n\nevent: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\nevent: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":1,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\nevent: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":1,\"delta\":{\"type\":\"text_delta\",\"text\":\"Found two events.\"}}\n\nevent: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":1}\n\nevent: message_delta\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":4}}\n\nevent: message_stop\ndata: {\"type\":\"message_stop\"}\n\n";
        context.Response.ContentType = "text/event-stream";
        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(sse), timeout.Token);
        context.Response.Close();
        await operation.WaitAsync(timeout.Token);
        Assert.That(delivered.Count(x => x.Type == ChatMessageTypes.AnthropicMcpToolListing), Is.EqualTo(1));
        var assistant = conversation.Messages.Last();
        Assert.That(assistant.Parts!.Count(x => x.Type == ChatMessageTypes.AnthropicMcpToolListing), Is.EqualTo(1));
        Assert.That(assistant.Parts[0].AnthropicMcpToolListing!.McpServerName, Is.EqualTo("calendar"));
        var replay = new ChatRequest { Model = ChatModel.Anthropic.Claude5.Opus55, Messages = conversation.Messages.ToList() };
        Assert.That(Body(replay)["messages"]?[1]?["content"]?[0]?["type"]?.ToString(), Is.EqualTo("mcp_tool_listing"));
    }

    private const string Response = """
        {"id":"msg_listing","type":"message","role":"assistant","model":"claude-opus-5-5","content":[
        {"type":"mcp_tool_listing","mcp_server_name":"calendar","tools":[{"name":"list_events","description":"List events","input_schema":{"type":"object","properties":{}}}]},
        {"type":"text","text":"Found two events."}],"stop_reason":"end_turn","usage":{"input_tokens":5,"output_tokens":4},
        "input_transformations":[{"type":"thinking_dropped","path":"messages.1.content.0","reason":"organization_binding_mismatch"},
        {"type":"thinking_mismatch_allowed","path":"messages.3.content.0","reason":"prefix_binding_mismatch"},
        {"type":"future_check","path":"messages.5.content.0","reason":"future_reason"}]}
        """;
}
