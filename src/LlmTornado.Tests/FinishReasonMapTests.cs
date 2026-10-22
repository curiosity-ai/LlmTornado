using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class FinishReasonMapTests
{
    [TestCase("STOP", ChatMessageFinishReasons.EndTurn)]
    [TestCase("MAX_TOKENS", ChatMessageFinishReasons.Length)]
    [TestCase("SAFETY", ChatMessageFinishReasons.ContentFilter)]
    [TestCase("MALFORMED_FUNCTION_CALL", ChatMessageFinishReasons.MalformedToolCall)]
    [TestCase("stop", ChatMessageFinishReasons.EndTurn)]
    [TestCase("tool_use", ChatMessageFinishReasons.ToolCalls)]
    [TestCase("pause_turn", ChatMessageFinishReasons.PauseTurn)]
    public void Map_ResolvesProviderFinishReasonsRegardlessOfCase(string reason, ChatMessageFinishReasons expected)
    {
        Assert.That(ChatMessageFinishReasonsConverter.Map.GetValueOrDefault(reason, ChatMessageFinishReasons.Unknown), Is.EqualTo(expected));
    }

    [Test]
    public async Task GoogleInboundStream_ReportsTheUppercaseFinishReasonGeminiSends()
    {
        const string json = """
            [
              {"candidates":[{"content":{"role":"model","parts":[{"text":"Hi"}]},"finishReason":"MAX_TOKENS","index":0}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":1,"totalTokenCount":6}}
            ]
            """;

        GoogleEndpointProvider provider = new GoogleEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        ChatResult? finish = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk?.StreamInternalKind is ChatResultStreamInternalKinds.FinishData)
            {
                finish = chunk;
            }
        }

        Assert.That(finish, Is.Not.Null);
        Assert.That(finish!.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.Length));
    }

    [Test]
    public async Task AnthropicInboundStream_ReportsPauseTurnAsTheFinishReason()
    {
        const string sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_pause","type":"message","role":"assistant","content":[],"model":"claude-sonnet-5-5","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":1}}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"pause_turn","stop_sequence":null},"usage":{"output_tokens":5}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        AnthropicEndpointProvider provider = new AnthropicEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        ChatResult? finish = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk?.StreamInternalKind is ChatResultStreamInternalKinds.FinishData)
            {
                finish = chunk;
            }
        }

        Assert.That(finish, Is.Not.Null);
        Assert.That(finish!.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.PauseTurn));
    }
}
