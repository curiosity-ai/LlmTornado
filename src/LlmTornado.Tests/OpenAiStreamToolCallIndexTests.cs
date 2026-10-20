using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class OpenAiStreamToolCallIndexTests
{
    [Test]
    public async Task InboundStream_DistinctIdsSharingIndexZeroStayApart()
    {
        // Ollama's OpenAI-compatible endpoint sends each call whole, all at index 0.
        List<ToolCall> calls = await CollectToolCalls(
            ToolChunk(0, "call_paris", "get_weather", "{\"city\":\"Paris\"}"),
            ToolChunk(0, "call_london", "get_weather", "{\"city\":\"London\"}"));

        Assert.That(calls.Select(x => (x.Id, x.FunctionCall!.Name, x.FunctionCall.Arguments)), Is.EqualTo(new[]
        {
            ("call_paris", "get_weather", "{\"city\":\"Paris\"}"),
            ("call_london", "get_weather", "{\"city\":\"London\"}")
        }));
    }

    [Test]
    public async Task InboundStream_FragmentWithoutIdContinuesTheLatestCallOfThatIndex()
    {
        List<ToolCall> calls = await CollectToolCalls(
            ToolChunk(0, "call_paris", "get_weather", "{\"city\":\"Paris\"}"),
            ToolChunk(0, "call_london", "get_weather", "{\"city\":"),
            ToolChunk(0, null, null, "\"London\"}"));

        Assert.That(calls.Select(x => (x.Id, x.FunctionCall!.Arguments)), Is.EqualTo(new[]
        {
            ("call_paris", "{\"city\":\"Paris\"}"),
            ("call_london", "{\"city\":\"London\"}")
        }));
    }

    [Test]
    public async Task InboundStream_FragmentRepeatingTheIdOfTheLatestCallContinuesIt()
    {
        List<ToolCall> calls = await CollectToolCalls(
            ToolChunk(0, "call_paris", "get_weather", "{\"city\":\"Paris\"}"),
            ToolChunk(0, "call_london", "get_weather", "{\"city\":"),
            ToolChunk(0, "call_london", null, "\"London\"}"));

        Assert.That(calls.Select(x => (x.Id, x.FunctionCall!.Arguments)), Is.EqualTo(new[]
        {
            ("call_paris", "{\"city\":\"Paris\"}"),
            ("call_london", "{\"city\":\"London\"}")
        }));
    }

    [Test]
    public async Task InboundStream_ArgumentsSplitAcrossChunksOfOneIndexAreJoined()
    {
        List<ToolCall> calls = await CollectToolCalls(
            ToolChunk(0, "call_paris", "get_weather", "{\"city\":"),
            ToolChunk(0, null, null, "\"Paris\"}"),
            ToolChunk(1, "call_london", "get_weather", "{\"city\":\"London\"}"));

        Assert.That(calls.Select(x => (x.Id, x.FunctionCall!.Arguments)), Is.EqualTo(new[]
        {
            ("call_paris", "{\"city\":\"Paris\"}"),
            ("call_london", "{\"city\":\"London\"}")
        }));
    }

    private static string ToolChunk(int index, string? id, string? name, string arguments)
    {
        JObject function = new JObject { ["arguments"] = arguments };
        JObject toolCall = new JObject { ["index"] = index };

        if (id is not null)
        {
            toolCall["id"] = id;
            toolCall["type"] = "function";
        }

        if (name is not null)
        {
            function["name"] = name;
        }

        toolCall["function"] = function;

        JObject chunk = new JObject
        {
            ["id"] = "chatcmpl-1",
            ["object"] = "chat.completion.chunk",
            ["created"] = 1,
            ["model"] = "llama3.2",
            ["choices"] = new JArray(new JObject
            {
                ["index"] = 0,
                ["delta"] = new JObject { ["tool_calls"] = new JArray(toolCall) },
                ["finish_reason"] = null
            })
        };

        return $"data: {chunk.ToString(Formatting.None)}\n\n";
    }

    private static async Task<List<ToolCall>> CollectToolCalls(params string[] chunks)
    {
        string sse = string.Concat(chunks) + """
            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"llama3.2","choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]}

            data: [DONE]


            """;

        OpenAiEndpointProvider provider = new OpenAiEndpointProvider(LLmProviders.Custom);
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "weather in Paris and London?")],
            Tools = [new Tool(new ToolFunction("get_weather", "Weather of a city"))]
        };

        ChatResult? last = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            last = chunk;
        }

        return last?.Choices?[0].Delta?.ToolCalls ?? [];
    }
}
