using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class CohereStreamToolCallIndexTests
{
    private const string Sse = """
        data: {"type":"tool-call-start","index":0,"delta":{"message":{"tool_calls":{"id":"call_a","type":"function","function":{"name":"get_weather","arguments":""}}}}}

        data: {"type":"tool-call-delta","index":0,"delta":{"message":{"tool_calls":{"function":{"arguments":"{\"city\":\"Paris\"}"}}}}}

        data: {"type":"tool-call-end","index":0}

        data: {"type":"tool-call-start","index":1,"delta":{"message":{"tool_calls":{"id":"call_b","type":"function","function":{"name":"get_weather","arguments":""}}}}}

        data: {"type":"tool-call-delta","index":1,"delta":{"message":{"tool_calls":{"function":{"arguments":"{\"city\":\"Rome\"}"}}}}}

        data: {"type":"tool-call-end","index":1}

        data: {"type":"message-end","delta":{"finish_reason":"TOOL_CALL","usage":{"billed_units":{"input_tokens":12,"output_tokens":3},"tokens":{"input_tokens":12,"output_tokens":3}}}}


        """;

    [Test]
    public async Task InboundStream_KeepsTheIndexOfEachToolCall()
    {
        CohereEndpointProvider provider = new CohereEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(Sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        List<int?> deltaIndexes = [];
        ChatStreamEventHandler handler = new ChatStreamEventHandler
        {
            FunctionCallDeltaHandler = update =>
            {
                deltaIndexes.Add(update.Index);
                return ValueTask.CompletedTask;
            }
        };

        List<ToolCall> calls = [];
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, handler))
        {
            if (chunk?.Choices?.FirstOrDefault()?.Delta?.ToolCalls is { } toolCalls)
            {
                calls.AddRange(toolCalls);
            }
        }

        Assert.That(calls.Select(x => x.Index), Is.EqualTo(new int?[] { 0, 1 }));
        Assert.That(deltaIndexes, Is.EqualTo(new int?[] { 0, 1 }));
    }
}
