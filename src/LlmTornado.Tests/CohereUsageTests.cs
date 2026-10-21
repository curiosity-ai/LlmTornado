using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class CohereUsageTests
{
    [Test]
    public async Task InboundStream_MessageEndReportsInputAndOutputTokensSeparately()
    {
        const string sse = """
            data: {"type":"content-delta","index":0,"delta":{"message":{"content":{"text":"Hi"}}}}

            data: {"type":"message-end","delta":{"finish_reason":"COMPLETE","usage":{"billed_units":{"input_tokens":120,"output_tokens":30},"tokens":{"input_tokens":150,"output_tokens":30}}}}


            """;

        CohereEndpointProvider provider = new CohereEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        ChatUsage? usage = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk?.Usage is not null)
            {
                usage = chunk.Usage;
            }
        }

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.PromptTokens, Is.EqualTo(120));
        Assert.That(usage.CompletionTokens, Is.EqualTo(30));
        Assert.That(usage.TotalTokens, Is.EqualTo(150));
    }

    [Test]
    public async Task InboundStream_MessageEndReportsCachedPromptTokensAsCacheReadTokens()
    {
        const string sse = """
            data: {"type":"content-delta","index":0,"delta":{"message":{"content":{"text":"Hi"}}}}

            data: {"type":"message-end","delta":{"finish_reason":"COMPLETE","usage":{"billed_units":{"input_tokens":120,"output_tokens":30},"tokens":{"input_tokens":150,"output_tokens":30},"cached_tokens":64}}}


            """;

        CohereEndpointProvider provider = new CohereEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        ChatUsage? usage = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk?.Usage is not null)
            {
                usage = chunk.Usage;
            }
        }

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.CacheReadTokens, Is.EqualTo(64));
        Assert.That(usage.PromptTokens, Is.EqualTo(120));
    }
}
