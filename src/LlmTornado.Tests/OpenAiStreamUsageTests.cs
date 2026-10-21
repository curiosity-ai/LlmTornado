using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class OpenAiStreamUsageTests
{
    [Test]
    public async Task InboundStream_TrailingEmptyChoicesChunkWithoutUsage_KeepsUsageFromFinishChunk()
    {
        List<ChatResult> chunks = await Stream("""
            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-x","choices":[{"index":0,"delta":{"role":"assistant","content":"Hi"}}]}

            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-x","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}

            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-x","choices":[],"usage":null}

            data: [DONE]


            """);

        Assert.That(chunks.Last().Usage?.TotalTokens, Is.EqualTo(5));
    }

    [Test]
    public async Task InboundStream_TrailingUsageChunk_IsReported()
    {
        List<ChatResult> chunks = await Stream("""
            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-x","choices":[{"index":0,"delta":{"role":"assistant","content":"Hi"}}]}

            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-x","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"gpt-x","choices":[],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}

            data: [DONE]


            """);

        Assert.That(chunks.Last().Usage?.TotalTokens, Is.EqualTo(5));
    }

    private static async Task<List<ChatResult>> Stream(string sse)
    {
        OpenAiEndpointProvider provider = new OpenAiEndpointProvider(LLmProviders.OpenAi);
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            StreamOptions = ChatStreamOptions.KnownOptionsIncludeUsage,
            Messages = [new ChatMessage(ChatMessageRoles.User, "hi")]
        };

        List<ChatResult> chunks = [];
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk is not null)
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }
}
