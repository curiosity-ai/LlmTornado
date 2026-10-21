using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicStreamStopSequenceTests
{
    [Test]
    public async Task InboundStream_MessageDeltaStopSequenceIsReturnedAsTheStopReasonOfTheFinishChunk()
    {
        const string sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_stop","type":"message","role":"assistant","content":[],"model":"claude-sonnet-5-5","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":1}}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"stop_sequence","stop_sequence":"END"},"usage":{"output_tokens":5}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        ChatChoice? choice = await CollectFinishChoice(sse);

        Assert.That(choice, Is.Not.Null);
        Assert.That(choice!.FinishReason, Is.EqualTo(ChatMessageFinishReasons.StopSequence));
        Assert.That(choice.StopReason, Is.EqualTo("END"));
    }

    [Test]
    public async Task InboundStream_NullStopSequenceLeavesTheStopReasonEmpty()
    {
        const string sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_stop","type":"message","role":"assistant","content":[],"model":"claude-sonnet-5-5","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":1}}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":5}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        ChatChoice? choice = await CollectFinishChoice(sse);

        Assert.That(choice, Is.Not.Null);
        Assert.That(choice!.StopReason, Is.Null);
    }

    private static async Task<ChatChoice?> CollectFinishChoice(string sse)
    {
        AnthropicEndpointProvider provider = new AnthropicEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        ChatResult? finishChunk = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk?.StreamInternalKind is ChatResultStreamInternalKinds.FinishData)
            {
                finishChunk = chunk;
            }
        }

        return finishChunk?.Choices?[0];
    }
}
