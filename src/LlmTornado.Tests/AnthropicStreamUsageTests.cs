using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicStreamUsageTests
{
    [Test]
    public async Task InboundStream_MessageDeltaReplacesTheStartUsageWithTheFinalCounts()
    {
        // With server tools, message_delta carries the final cumulative input and cache counts.
        const string sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_usage","type":"message","role":"assistant","content":[],"model":"claude-sonnet-5-5","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":1}}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"input_tokens":120,"cache_creation_input_tokens":20,"cache_read_input_tokens":100,"output_tokens":50}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        ChatUsage? usage = await CollectFinishUsage(sse);

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.PromptTokens, Is.EqualTo(120));
        Assert.That(usage.CompletionTokens, Is.EqualTo(50));
        Assert.That(usage.CacheCreationTokens, Is.EqualTo(20));
        Assert.That(usage.CacheReadTokens, Is.EqualTo(100));
        Assert.That(usage.TotalTokens, Is.EqualTo(170));
    }

    [Test]
    public async Task InboundStream_MessageDeltaWithOnlyOutputTokensKeepsTheStartCounts()
    {
        const string sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_usage","type":"message","role":"assistant","content":[],"model":"claude-sonnet-5-5","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"cache_creation_input_tokens":3,"cache_read_input_tokens":7,"output_tokens":1}}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":25}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        ChatUsage? usage = await CollectFinishUsage(sse);

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.PromptTokens, Is.EqualTo(10));
        Assert.That(usage.CompletionTokens, Is.EqualTo(25));
        Assert.That(usage.CacheCreationTokens, Is.EqualTo(3));
        Assert.That(usage.CacheReadTokens, Is.EqualTo(7));
        Assert.That(usage.TotalTokens, Is.EqualTo(35));
    }

    private static async Task<ChatUsage?> CollectFinishUsage(string sse)
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

        return finishChunk?.Usage;
    }
}
