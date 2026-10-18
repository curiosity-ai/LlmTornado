using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicStreamThinkingPartsTests
{
    private const string ThinkingTextToolUseSse = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_thinking_tool","type":"message","role":"assistant","content":[],"model":"claude-sonnet-4-6","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":420,"output_tokens":4}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"The user wants the weather in Paris."}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig_thinking_0"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: content_block_start
        data: {"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"Let me check the weather."}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":1}

        event: content_block_start
        data: {"type":"content_block_start","index":2,"content_block":{"type":"tool_use","id":"toolu_01","name":"get_weather","input":{}}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":2,"delta":{"type":"input_json_delta","partial_json":"{\"city\": \"Paris\"}"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":2}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":61}}

        event: message_stop
        data: {"type":"message_stop"}

        """;

    // Citations split the answer into several text blocks.
    private const string ThinkingThreeTextBlocksSse = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_thinking_text","type":"message","role":"assistant","content":[],"model":"claude-sonnet-4-6","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":120,"output_tokens":4}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"Answer from the document."}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig_thinking_0"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: content_block_start
        data: {"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"According to the document, "}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":1}

        event: content_block_start
        data: {"type":"content_block_start","index":2,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":2,"delta":{"type":"text_delta","text":"the grass is green"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":2}

        event: content_block_start
        data: {"type":"content_block_start","index":3,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":3,"delta":{"type":"text_delta","text":"."}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":3}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":30}}

        event: message_stop
        data: {"type":"message_stop"}

        """;

    [Test]
    public async Task InboundStream_ThinkingTextToolUse_AppendsReasoningOnce()
    {
        List<ChatResult?> chunks = await CollectStreamChunks(ThinkingTextToolUseSse);

        ChatMessage? appended = chunks.Last(x => x?.StreamInternalKind is ChatResultStreamInternalKinds.AppendAssistantMessage)!.Choices![0].Delta;
        List<ChatMessagePart> reasoning = appended!.Parts!.Where(x => x.Type is ChatMessageTypes.Reasoning).ToList();

        Assert.That(reasoning, Has.Count.EqualTo(1));
        Assert.That(reasoning[0].Reasoning!.Content, Is.EqualTo("The user wants the weather in Paris."));
        Assert.That(reasoning[0].Reasoning!.Signature, Is.EqualTo("sig_thinking_0"));
    }

    [Test]
    public async Task InboundStream_ThinkingThenSeveralTextBlocks_AppendsReasoningOnce()
    {
        List<ChatResult?> chunks = await CollectStreamChunks(ThinkingThreeTextBlocksSse);

        ChatMessage? appended = chunks.Last(x => x?.StreamInternalKind is ChatResultStreamInternalKinds.AppendAssistantMessage)!.Choices![0].Delta;

        Assert.That(appended!.Content, Is.EqualTo("According to the document, the grass is green."));
        Assert.That(appended.Parts!.Count(x => x.Type is ChatMessageTypes.Reasoning), Is.EqualTo(1));
    }

    [Test]
    public async Task InboundStream_ThinkingThenSeveralTextBlocks_FinishesThinkingBlockOnce()
    {
        List<ChatResult?> chunks = await CollectStreamChunks(ThinkingThreeTextBlocksSse);

        Assert.That(chunks.Count(x => x?.StreamInternalKind is ChatResultStreamInternalKinds.AssistantMessageTransientBlock), Is.EqualTo(1));
    }

    private static async Task<List<ChatResult?>> CollectStreamChunks(string sse)
    {
        AnthropicEndpointProvider provider = new AnthropicEndpointProvider();
        MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        StreamReader reader = new StreamReader(stream);
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        List<ChatResult?> chunks = [];
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            chunks.Add(chunk);
        }

        return chunks;
    }
}
