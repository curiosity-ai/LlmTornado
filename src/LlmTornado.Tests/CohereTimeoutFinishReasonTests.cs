using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class CohereTimeoutFinishReasonTests
{
    [TestCase("TIMEOUT", ChatMessageFinishReasons.Error)]
    [TestCase("MAX_TOKENS", ChatMessageFinishReasons.Length)]
    [TestCase("COMPLETE", ChatMessageFinishReasons.EndTurn)]
    public async Task InboundStream_MessageEndMapsTheFinishReason(string cohereReason, ChatMessageFinishReasons expected)
    {
        string sse = """
            data: {"type":"content-delta","index":0,"delta":{"message":{"content":{"text":"Hi"}}}}

            data: {"type":"message-end","delta":{"finish_reason":"__REASON__","usage":{"billed_units":{"input_tokens":12,"output_tokens":3},"tokens":{"input_tokens":15,"output_tokens":3}}}}


            """.Replace("__REASON__", cohereReason);

        CohereEndpointProvider provider = new CohereEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        ChatMessageFinishReasons? finishReason = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            ChatChoice? choice = chunk?.Choices?.FirstOrDefault();

            if (choice?.FinishReason is not null)
            {
                finishReason = choice.FinishReason;
            }
        }

        Assert.That(finishReason, Is.EqualTo(expected));
    }

    [TestCase("TIMEOUT", ChatMessageFinishReasons.Error)]
    [TestCase("MAX_TOKENS", ChatMessageFinishReasons.Length)]
    [TestCase("COMPLETE", ChatMessageFinishReasons.EndTurn)]
    public void Response_MapsTheFinishReason(string cohereReason, ChatMessageFinishReasons expected)
    {
        string json = """
            {"id":"c1","finish_reason":"__REASON__","message":{"role":"assistant","content":[{"type":"text","text":"Hi"}]},"usage":{"billed_units":{"input_tokens":12,"output_tokens":3},"tokens":{"input_tokens":15,"output_tokens":3}}}
            """.Replace("__REASON__", cohereReason);

        ChatResult? result = VendorCohereChatResult.Deserialize(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Choices![0].FinishReason, Is.EqualTo(expected));
    }
}
