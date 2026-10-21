using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicThinkingReplayTests
{
    private static IEndpointProvider Provider => new TornadoApi(LLmProviders.Anthropic, "test-key").GetProvider(LLmProviders.Anthropic);

    private const string MixedBlocks = """
        [{"type":"mcp_tool_listing","mcp_server_name":"calendar","tools":[]},
         {"type":"thinking","thinking":"  Check the first record.\n","signature":"sig_first"},
         {"type":"text","text":"Checking. "},
         {"type":"tool_use","id":"tool_first","name":"lookup","input":{"key":"first"}},
         {"type":"thinking","thinking":"The next record may differ.","signature":"sig_second"},
         {"type":"redacted_thinking","data":"  opaque/encrypted+payload==\n"},
         {"type":"tool_use","id":"tool_second","name":"lookup","input":{"key":"second"}},
         {"type":"text","text":"Both requested."}]
        """;

    private static string Response(JArray blocks, string stopReason = "end_turn") => new JObject
    {
        ["id"] = "msg_replay", ["type"] = "message", ["role"] = "assistant", ["model"] = "claude-opus-5-5",
        ["content"] = blocks, ["stop_reason"] = stopReason,
        ["usage"] = new JObject { ["input_tokens"] = 10, ["output_tokens"] = 20 }
    }.ToString(Formatting.None);

    private static ChatMessage Parse(JArray blocks, string stopReason = "end_turn") =>
        Provider.InboundMessage<ChatResult>(Response(blocks, stopReason), null, null)!.Choices!.Single().Message!;

    private static JArray Replay(ChatMessage message)
    {
        ChatRequest request = new()
        {
            Model = ChatModel.Anthropic.Claude5.Opus55,
            Messages = [new(ChatMessageRoles.User, "Check records."), message, new(ChatMessageRoles.User, "Continue.")]
        };
        return (JArray)JObject.Parse(request.Serialize(Provider).Body.ToString()!)["messages"]![1]!["content"]!;
    }

    [TestCase(AnthropicThinkingPrefixMismatchBehavior.Reject, "error")]
    [TestCase(AnthropicThinkingPrefixMismatchBehavior.Drop, "drop_block")]
    public void BindingControls_UseCanonicalValuesAndSelectBeta(AnthropicThinkingPrefixMismatchBehavior behavior, string expected)
    {
        ChatRequest request = BindingRequest("claude-sonnet-5-5", AnthropicThinkingTypes.Adaptive, behavior);
        JObject body = JObject.Parse(request.Serialize(Provider).Body.ToString()!);
        Assert.That(body["thinking"]?["block_binding"]?["prefix_mismatch_behavior"]?.ToString(), Is.EqualTo(expected));
        using HttpRequestMessage outbound = Provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, false, request);
        Assert.That(string.Join(",", outbound.Headers.GetValues("anthropic-beta")), Does.Contain("thinking-binding-controls-2026-08-01"));
    }

    [TestCase("reject", AnthropicThinkingPrefixMismatchBehavior.Reject)]
    [TestCase("drop", AnthropicThinkingPrefixMismatchBehavior.Drop)]
    [TestCase("error", AnthropicThinkingPrefixMismatchBehavior.Reject)]
    [TestCase("drop_block", AnthropicThinkingPrefixMismatchBehavior.Drop)]
    public void BindingControls_ReadExistingSavedEnumNames(string jsonValue, AnthropicThinkingPrefixMismatchBehavior expected)
    {
        var restored = JsonConvert.DeserializeObject<AnthropicThinkingBlockBinding>("{\"prefix_mismatch_behavior\":\"" + jsonValue + "\"}");
        Assert.That(restored?.PrefixMismatchBehavior, Is.EqualTo(expected));
    }

    [TestCase("claude-sonnet-5-5", AnthropicThinkingTypes.BetweenTools)]
    [TestCase("claude-sonnet-5-5", AnthropicThinkingTypes.Disabled)]
    [TestCase("claude-haiku-5-5", AnthropicThinkingTypes.Disabled)]
    public void BindingControls_RejectModesThatCannotEnforceTheSetting(string model, AnthropicThinkingTypes mode)
    {
        Assert.Throws<ArgumentException>(() => BindingRequest(model, mode, AnthropicThinkingPrefixMismatchBehavior.Drop).Serialize(Provider));
    }

    [Test]
    public void AlwaysOnThinking_PreservesExplicitBindingControls()
    {
        JObject body = JObject.Parse(BindingRequest("claude-opus-5-5", AnthropicThinkingTypes.Disabled, AnthropicThinkingPrefixMismatchBehavior.Drop).Serialize(Provider).Body.ToString()!);
        Assert.That(body["thinking"]?["type"]?.ToString(), Is.EqualTo("adaptive"));
        Assert.That(body["thinking"]?["block_binding"]?["prefix_mismatch_behavior"]?.ToString(), Is.EqualTo("drop_block"));
    }

    private static ChatRequest BindingRequest(string model, AnthropicThinkingTypes mode, AnthropicThinkingPrefixMismatchBehavior behavior) => new()
    {
        Model = model, Messages = [new(ChatMessageRoles.User, "Continue.")],
        VendorExtensions = new(new ChatRequestVendorAnthropicExtensions
        {
            Thinking = new() { Type = mode, BlockBinding = new() { PrefixMismatchBehavior = behavior } }
        })
    };

    [TestCase("[{\"type\":\"thinking\",\"thinking\":\"  Unfinished\\n\",\"signature\":\"sig_only\"}]")]
    [TestCase("[{\"type\":\"thinking\",\"thinking\":\"\",\"signature\":\"sig_omitted\"}]")]
    [TestCase("[{\"type\":\"redacted_thinking\",\"data\":\"  opaque+data/==\\n\"}]")]
    [TestCase("[{\"type\":\"thinking\",\"thinking\":\"First\",\"signature\":\"a\"},{\"type\":\"thinking\",\"thinking\":\"Second\",\"signature\":\"b\"}]")]
    public void ThinkingOnlyJson_ReplaysOpaqueValuesAndBlockOrder(string json)
    {
        JArray original = JArray.Parse(json);
        ChatMessage message = Parse(original, "max_tokens");
        Assert.That(message.Role, Is.EqualTo(ChatMessageRoles.Assistant));
        Assert.That(message.Content, Is.Null);
        Assert.That(message.Parts!.All(p => p.Type == ChatMessageTypes.Reasoning), Is.True);
        Assert.That(JToken.DeepEquals(Replay(message), original), Is.True);
        if (original[0]?["type"]?.ToString() == "redacted_thinking") Assert.That(message.Parts[0].Reasoning?.IsRedacted, Is.True);
        else if (original[0]?["thinking"]?.ToString() == "") Assert.That(message.Parts[0].Reasoning?.IsOmitted, Is.True);
    }

    [Test]
    public void Json_MixedThinkingTextAndCalls_RetainsOneOrderedAssistantTurn()
    {
        JArray original = JArray.Parse(MixedBlocks);
        ChatMessage message = Parse(original, "tool_use");
        Assert.That(message.Content, Is.EqualTo("Checking. Both requested."));
        Assert.That(message.ToolCalls?.Select(x => x.Id), Is.EqualTo(new[] { "tool_first", "tool_second" }));
        Assert.That(JToken.DeepEquals(Replay(message), original), Is.True);
    }

    [Test]
    public void PersistedPartsAndCalls_RetainToolPositionsAndUpdatedArguments()
    {
        ChatMessage message = Parse(JArray.Parse(MixedBlocks));
        ChatMessage restored = new(ChatMessageRoles.Assistant)
        {
            Parts = JsonConvert.DeserializeObject<List<ChatMessagePart>>(JsonConvert.SerializeObject(message.Parts)),
            ToolCalls = JsonConvert.DeserializeObject<List<ToolCall>>(JsonConvert.SerializeObject(message.ToolCalls))
        };
        Assert.That(JToken.DeepEquals(Replay(restored), JArray.Parse(MixedBlocks)), Is.True, Replay(restored).ToString());
        Assert.That(restored.Parts!.Single(x => x.Reasoning?.Content is null && x.Reasoning?.Signature is not null).Reasoning!.IsRedacted, Is.True);
        restored.ToolCalls![0].FunctionCall!.Arguments = "{\"key\":\"edited\"}";
        Assert.That(Replay(restored)[3]?["input"]?["key"]?.ToString(), Is.EqualTo("edited"), "Positions reference the public tool-call list, rather than a stale payload copy.");
    }

    [Test]
    public void AdditionalUnpositionedCalls_AreWrittenOnceAfterOrderedContent()
    {
        ChatMessage message = Parse(JArray.Parse(MixedBlocks));
        message.ToolCalls!.Add(new() { Id = "extra", FunctionCall = new() { Name = "lookup", Arguments = "{}" } });
        JArray replay = Replay(message);
        Assert.That(replay.Where(x => x["type"]?.ToString() == "tool_use").Select(x => x["id"]?.ToString()), Is.EqualTo(new[] { "tool_first", "tool_second", "extra" }));
        Assert.That(replay.Last!["id"]?.ToString(), Is.EqualTo("extra"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ToolPositions_RejectMissingOrDuplicateReferences(bool duplicate)
    {
        ChatMessage message = Parse(JArray.Parse(MixedBlocks));
        if (duplicate) message.Parts!.Add(new(ChatMessageTypes.AnthropicToolUse) { AnthropicToolUseId = "tool_first" });
        else message.ToolCalls!.RemoveAt(0);
        Assert.Throws<ArgumentException>(() => Replay(message));
    }

    [Test]
    public void ThinkingBeforeServerToolOutput_RemainsTypedIncludingRedaction()
    {
        JArray original = JArray.Parse("""[{"type":"thinking","thinking":"Before tool","signature":"sig"},{"type":"redacted_thinking","data":"opaque"},{"type":"web_search_tool_result","tool_use_id":"server_1","content":[]},{"type":"text","text":"Answer"}]""");
        ChatResult result = Provider.InboundMessage<ChatResult>(Response(original), null, null)!;
        var reasoning = result.Choices!.SelectMany(x => x.Message?.Parts ?? []).Where(x => x.Type == ChatMessageTypes.Reasoning).ToList();
        Assert.That(reasoning, Has.Count.EqualTo(2));
        Assert.That(reasoning[0].Reasoning?.Signature, Is.EqualTo("sig"));
        Assert.That(reasoning[1].Reasoning?.Content, Is.Null);
        Assert.That(reasoning[1].Reasoning?.Signature, Is.EqualTo("opaque"));
        Assert.That(result.Choices.SelectMany(x => x.Message?.Parts ?? []).Any(x => x.Text?.Contains("REDACTED THINKING") == true), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Stream_InterleavedCallsAndThinking_ReplaysOriginalBlocks(bool wholeToolInputs)
    {
        JArray original = JArray.Parse(MixedBlocks);
        List<ChatResult> chunks = await Collect(Stream(original, wholeToolInputs));
        ChatMessage appended = chunks.Single(x => x.StreamInternalKind == ChatResultStreamInternalKinds.AppendAssistantMessage).Choices![0].Delta!;
        var notification = chunks.Single(x => x.StreamInternalKind is null && x.Choices?.FirstOrDefault()?.Delta?.ToolCalls?.Count > 0).Choices![0].Delta!;
        Assert.That(JToken.DeepEquals(Replay(appended), original), Is.True, Replay(appended).ToString());
        Assert.That(JToken.DeepEquals(Replay(notification), original), Is.True, "Rich conversations stop reading at the tool notification, so it must carry the complete turn.");
        Assert.That(chunks.Count(x => x.StreamInternalKind == ChatResultStreamInternalKinds.AssistantMessageTransientBlock), Is.EqualTo(2));
        Assert.That(appended.Parts!.Where(x => x.Type == ChatMessageTypes.Reasoning).Select(x => x.Reasoning!.Signature),
            Is.EqualTo(new[] { "sig_first", "sig_second", "  opaque/encrypted+payload==\n" }));
        Assert.That(appended.ToolCalls![0].FunctionCall!.Arguments, Does.Contain("first"));
        Assert.That(appended.ToolCalls[1].FunctionCall!.Arguments, Does.Contain("second"));
    }

    [TestCase("[{\"type\":\"thinking\",\"thinking\":\"Only thought\",\"signature\":\"sig_only\"}]")]
    [TestCase("[{\"type\":\"thinking\",\"thinking\":\"\",\"signature\":\"sig_omitted\"}]")]
    [TestCase("[{\"type\":\"redacted_thinking\",\"data\":\"opaque\"}]")]
    public async Task Stream_WithoutText_RetainsThinkingOnlyHistory(string json)
    {
        JArray original = JArray.Parse(json);
        var chunks = await Collect(Stream(original));
        ChatMessage appended = chunks.Single(x => x.StreamInternalKind == ChatResultStreamInternalKinds.AppendAssistantMessage).Choices![0].Delta!;
        Assert.That(JToken.DeepEquals(Replay(appended), original), Is.True);
    }

    [Test]
    public async Task Stream_WholeBlocksAtStart_PreserveInitialThinkingTextAndSignatures()
    {
        const string sse = "event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"thinking\",\"thinking\":\"  Whole thought\\n\",\"signature\":\"whole_signature\"}}\n\nevent: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\nevent: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":1,\"content_block\":{\"type\":\"text\",\"text\":\"Whole answer.\"}}\n\nevent: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":1}\n\n";
        var chunks = await Collect(sse);
        var message = chunks.Single(x => x.StreamInternalKind == ChatResultStreamInternalKinds.AppendAssistantMessage).Choices![0].Delta!;
        var original = JArray.Parse("""[{"type":"thinking","thinking":"  Whole thought\n","signature":"whole_signature"},{"type":"text","text":"Whole answer."}]""");
        Assert.That(JToken.DeepEquals(Replay(message), original), Is.True);
        Assert.That(chunks.Where(x => x.StreamInternalKind is null).Select(x => x.Choices?.FirstOrDefault()?.Delta?.Content).Where(x => x is not null), Is.EqualTo(new[] { "Whole answer." }));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task Conversation_RichHistoryReplaysTheCompleteTurnOnFollowUp(bool streaming, bool resolveTools)
    {
        using LocalServer server = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        var conversation = server.Api.Chat.CreateConversation(ChatModel.Anthropic.Claude5.Opus55);
        conversation.AppendUserInput("Check records.");
        JArray original = JArray.Parse(MixedBlocks);
        List<string> resolved = [];
        ValueTask Resolve(List<FunctionCall> calls)
        {
            foreach (FunctionCall call in calls)
            {
                resolved.Add(call.ToolCall!.Id!);
                call.Resolve("Done.", true);
            }
            return ValueTask.CompletedTask;
        }
        Task first;
        if (streaming) first = conversation.StreamResponseRich(new ChatStreamEventHandler { FunctionCallHandler = resolveTools ? Resolve : null }, timeout.Token);
        else first = resolveTools ? conversation.GetResponseRich(Resolve, timeout.Token) : conversation.GetResponseRich(timeout.Token);
        await server.Respond(streaming ? Stream(original) : Response(original, "tool_use"), streaming, timeout.Token);
        await first.WaitAsync(timeout.Token);
        Assert.That(conversation.Messages.Count(x => x.Role == ChatMessageRoles.Assistant), Is.EqualTo(1));
        if (resolveTools) Assert.That(resolved, Is.EqualTo(new[] { "tool_first", "tool_second" }));
        else foreach (string id in new[] { "tool_first", "tool_second" }) conversation.AddToolMessage(id, "Done.");
        Task followUp = conversation.GetResponseRich(timeout.Token);
        JObject sent = await server.Respond(Response(JArray.Parse("""[{"type":"text","text":"Done."}]""")), false, timeout.Token);
        await followUp.WaitAsync(timeout.Token);
        Assert.That(JToken.DeepEquals(sent["messages"]![1]!["content"], original), Is.True);
        Assert.That(sent["messages"]![1]!["role"]?.ToString(), Is.EqualTo("assistant"));
        Assert.That(sent["messages"]![2]!["content"]!.Select(x => x["tool_use_id"]?.ToString()), Is.EqualTo(new[] { "tool_first", "tool_second" }));
    }

    [Test]
    public async Task Conversation_PlainStream_DeliversTextOnceAndKeepsSignedHistoryOnce()
    {
        using LocalServer server = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        var conversation = server.Api.Chat.CreateConversation(ChatModel.Anthropic.Claude5.Opus55);
        conversation.AppendUserInput("Check records.");
        Task<string> operation = Task.Run(async () =>
        {
            StringBuilder text = new();
            await foreach (string fragment in conversation.StreamResponseEnumerable(token: timeout.Token)) text.Append(fragment);
            return text.ToString();
        }, timeout.Token);
        JArray original = JArray.Parse(MixedBlocks);
        await server.Respond(Stream(original), true, timeout.Token);
        Assert.That(await operation.WaitAsync(timeout.Token), Is.EqualTo("Checking. Both requested."));
        ChatMessage assistant = conversation.Messages.Single(x => x.Role == ChatMessageRoles.Assistant);
        Assert.That(JToken.DeepEquals(Replay(assistant), original), Is.True);
    }

    private static async Task<List<ChatResult>> Collect(string sse)
    {
        using StreamReader reader = new(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        List<ChatResult> chunks = [];
        await foreach (ChatResult? chunk in new AnthropicEndpointProvider().InboundStream(reader, new() { Model = ChatModel.Anthropic.Claude5.Opus55 }, null))
            if (chunk is not null) chunks.Add(chunk);
        return chunks;
    }

    private static string Stream(JArray blocks, bool wholeToolInputs = false)
    {
        StringBuilder stream = new();
        void Emit(string type, JObject data)
        {
            data["type"] = type;
            stream.Append("event: ").Append(type).Append("\ndata: ").Append(data.ToString(Formatting.None)).Append("\n\n");
        }
        Emit("message_start", new() { ["message"] = JObject.Parse(Response([])) });
        for (int index = 0; index < blocks.Count; index++)
        {
            JObject block = (JObject)blocks[index]!.DeepClone();
            string type = block["type"]!.ToString();
            JObject start = (JObject)block.DeepClone();
            if (type == "text") start["text"] = "";
            if (type == "thinking") { start["thinking"] = ""; start["signature"] = ""; }
            if (type == "tool_use" && !wholeToolInputs) start["input"] = new JObject();
            Emit("content_block_start", new() { ["index"] = index, ["content_block"] = start });
            void Delta(JObject delta) => Emit("content_block_delta", new() { ["index"] = index, ["delta"] = delta });
            switch (type)
            {
                case "text": Delta(new() { ["type"] = "text_delta", ["text"] = block["text"] }); break;
                case "thinking":
                    if (!string.IsNullOrEmpty(block["thinking"]?.ToString())) Delta(new() { ["type"] = "thinking_delta", ["thinking"] = block["thinking"] });
                    string signature = block["signature"]!.ToString();
                    int split = signature.Length / 2;
                    Delta(new() { ["type"] = "signature_delta", ["signature"] = signature[..split] });
                    Delta(new() { ["type"] = "signature_delta", ["signature"] = signature[split..] });
                    break;
                case "tool_use" when !wholeToolInputs:
                    string input = block["input"]!.ToString(Formatting.None);
                    int midpoint = input.Length / 2;
                    Delta(new() { ["type"] = "input_json_delta", ["partial_json"] = input[..midpoint] });
                    Delta(new() { ["type"] = "input_json_delta", ["partial_json"] = input[midpoint..] });
                    break;
            }
            Emit("content_block_stop", new() { ["index"] = index });
        }
        Emit("message_delta", new()
        {
            ["delta"] = new JObject { ["stop_reason"] = blocks.Any(x => x["type"]?.ToString() == "tool_use") ? "tool_use" : "end_turn" },
            ["usage"] = new JObject { ["output_tokens"] = 20 }
        });
        Emit("message_stop", new());
        return stream.ToString();
    }

    private sealed class LocalServer : IDisposable
    {
        private readonly HttpListener listener = new();
        internal TornadoApi Api { get; }
        internal LocalServer()
        {
            using TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            string prefix = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Add(prefix);
            listener.Start();
            Api = new(new AnthropicEndpointProvider { Auth = new(LLmProviders.Anthropic, "test-key"), UrlResolver = (_, _, _) => prefix + "v1/{0}{1}" });
        }
        internal async Task<JObject> Respond(string response, bool streaming, CancellationToken cancellationToken)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            JObject request = JObject.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync(cancellationToken));
            Assert.That(context.Request.Url!.AbsolutePath, Is.EqualTo("/v1/messages"));
            context.Response.ContentType = streaming ? "text/event-stream" : "application/json";
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response), cancellationToken);
            context.Response.Close();
            return request;
        }
        public void Dispose() => listener.Close();
    }
}
