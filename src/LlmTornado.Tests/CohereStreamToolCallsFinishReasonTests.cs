using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class CohereStreamToolCallsFinishReasonTests
{
    private static async Task<List<ChatMessageFinishReasons?>> ReadFinishReasons(string finishReason)
    {
        string sse = """
            data: {"type":"tool-call-start","index":0,"delta":{"message":{"tool_calls":{"id":"call_a","type":"function","function":{"name":"get_weather","arguments":""}}}}}

            data: {"type":"tool-call-delta","index":0,"delta":{"message":{"tool_calls":{"function":{"arguments":"{\"city\":\"Par"}}}}}

            data: {"type":"message-end","delta":{"finish_reason":"FINISH_REASON","usage":{"billed_units":{"input_tokens":12,"output_tokens":3},"tokens":{"input_tokens":12,"output_tokens":3}}}}


            """.Replace("FINISH_REASON", finishReason);

        CohereEndpointProvider provider = new CohereEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        List<ChatMessageFinishReasons?> reasons = [];
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk?.Choices?.FirstOrDefault() is { FinishReason: not null } choice)
            {
                reasons.Add(choice.FinishReason);
            }
        }

        return reasons;
    }

    [Test]
    public async Task InboundStream_KeepsTheLengthFinishReasonOfTruncatedToolCalls()
    {
        List<ChatMessageFinishReasons?> reasons = await ReadFinishReasons("MAX_TOKENS");

        Assert.That(reasons, Is.All.EqualTo(ChatMessageFinishReasons.Length));
    }

    [Test]
    public async Task InboundStream_ReportsToolCallsForCompleteToolCalls()
    {
        List<ChatMessageFinishReasons?> reasons = await ReadFinishReasons("TOOL_CALL");

        Assert.That(reasons, Is.All.EqualTo(ChatMessageFinishReasons.ToolCalls));
    }
}
