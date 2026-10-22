using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class CohereFinishReasonTests
{
    [Test]
    public async Task InboundStream_ReportsAStopSequenceFinishReason()
    {
        const string sse = """
            data: {"type":"content-delta","index":0,"delta":{"message":{"content":{"text":"Hi"}}}}

            data: {"type":"message-end","delta":{"finish_reason":"STOP_SEQUENCE","usage":{"billed_units":{"input_tokens":12,"output_tokens":3},"tokens":{"input_tokens":12,"output_tokens":3}}}}


            """;

        CohereEndpointProvider provider = new CohereEndpointProvider();
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
        Assert.That(finish!.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.StopSequence));
    }

    [Test]
    public void Deserialize_ReportsAStopSequenceFinishReason()
    {
        const string json = """
            {"id":"c1","finish_reason":"STOP_SEQUENCE","message":{"role":"assistant","content":[{"type":"text","text":"Hi"}]},
             "usage":{"billed_units":{"input_tokens":12,"output_tokens":3},"tokens":{"input_tokens":12,"output_tokens":3}}}
            """;

        ChatResult? result = VendorCohereChatResult.Deserialize(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.StopSequence));
    }
}
