using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// A cache breakpoint placed on a tool-result message must reach the wire on its tool_result block;
/// otherwise every conversation whose tail is a tool result loses its incremental history breakpoint.
/// </summary>
[TestFixture]
public class AnthropicToolResultCacheControlTests
{
    [Test]
    public void ToolResult_WithCacheMarkedPart_SerializesCacheControlOnTheBlock()
    {
        JObject body = Serialize(ToolMessage(withCache: true));

        JToken toolResult = LastToolResultBlock(body);
        Assert.That(toolResult["type"]?.ToString(), Is.EqualTo("tool_result"));
        Assert.That(toolResult["tool_use_id"]?.ToString(), Is.EqualTo("toolu_1"));
        Assert.That(toolResult["cache_control"]?["type"]?.ToString(), Is.EqualTo("ephemeral"));
    }

    [Test]
    public void ToolResult_WithoutCacheMarkedPart_HasNoCacheControl()
    {
        JObject body = Serialize(ToolMessage(withCache: false));

        Assert.That(LastToolResultBlock(body)["cache_control"], Is.Null);
    }

    private static ChatMessage ToolMessage(bool withCache)
    {
        ChatMessage tool = new ChatMessage(ChatMessageRoles.Tool, "{\"ok\":true}")
        {
            ToolCallId = "toolu_1"
        };

        if (withCache)
        {
            tool.Parts =
            [
                new ChatMessagePart("{\"ok\":true}", new ChatMessagePartAnthropicExtensions
                {
                    Cache = AnthropicCacheSettings.Ephemeral
                })
            ];
        }

        return tool;
    }

    private static JObject Serialize(ChatMessage toolMessage)
    {
        TornadoApi api = new TornadoApi(LLmProviders.Anthropic, "test-key");
        IEndpointProvider provider = api.GetProvider(LLmProviders.Anthropic);

        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude46.Sonnet,
            MaxTokens = 64,
            Messages =
            [
                new ChatMessage(ChatMessageRoles.User, "call the tool"),
                new ChatMessage(ChatMessageRoles.Assistant)
                {
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "toolu_1",
                            FunctionCall = new FunctionCall { Name = "probe", Arguments = "{}" }
                        }
                    ]
                },
                toolMessage
            ]
        };

        TornadoRequestContent serialized = request.Serialize(provider);
        return JObject.Parse(serialized.Body.ToString()!);
    }

    private static JToken LastToolResultBlock(JObject body)
    {
        JToken? block = body["messages"]?
            .Last?["content"]?
            .LastOrDefault(b => b["type"]?.ToString() == "tool_result");

        Assert.That(block, Is.Not.Null, body.ToString());
        return block!;
    }
}
