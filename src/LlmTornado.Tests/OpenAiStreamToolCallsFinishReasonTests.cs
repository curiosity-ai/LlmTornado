using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;

namespace LlmTornado.Tests;

[TestFixture]
public class OpenAiStreamToolCallsFinishReasonTests
{
    [TestCase("tool_calls", ChatMessageFinishReasons.ToolCalls)]
    [TestCase("stop", ChatMessageFinishReasons.ToolCalls)]
    [TestCase("length", ChatMessageFinishReasons.Length)]
    [TestCase("content_filter", ChatMessageFinishReasons.ContentFilter)]
    public async Task InboundStream_ToolCallsKeepTheLengthAndContentFilterFinishReasons(string finishReason, ChatMessageFinishReasons expected)
    {
        // The arguments of a call cut off by max_tokens are truncated JSON, so the stream must not report it as a normal tool call.
        string sse = """
            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-5.6-terra","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"get_weather","arguments":"{\"city\":"}}]},"finish_reason":null}]}

            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-5.6-terra","choices":[{"index":0,"delta":{},"finish_reason":"__REASON__"}]}

            data: [DONE]


            """.Replace("__REASON__", finishReason);

        OpenAiEndpointProvider provider = new OpenAiEndpointProvider(LLmProviders.OpenAi);
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "weather in Paris?")],
            Tools = [new Tool(new ToolFunction("get_weather", "Weather of a city"))]
        };

        ChatResult? last = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            last = chunk;
        }

        Assert.That(last?.Choices?[0].Delta?.ToolCalls, Has.Count.EqualTo(1));
        Assert.That(last!.Choices![0].FinishReason, Is.EqualTo(expected));
    }
}
