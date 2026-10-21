using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Perplexity;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class PerplexityStreamVendorExtensionsTests
{
    [Test]
    public async Task InboundStream_KeepsSearchResultsAndRelatedQuestions()
    {
        List<ChatResult> chunks = await Stream("""
            data: {"id":"p1","object":"chat.completion","created":1,"model":"sonar","choices":[{"index":0,"delta":{"role":"assistant","content":"Hel"}}],"usage":{"prompt_tokens":3,"completion_tokens":1,"total_tokens":4}}

            data: {"id":"p1","object":"chat.completion","created":1,"model":"sonar","search_results":[{"id":1,"title":"Example","url":"https://example.com"}],"related_questions":["What next?"],"choices":[{"index":0,"delta":{"content":"lo"}}],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}

            data: {"id":"p1","object":"chat.completion","created":1,"model":"sonar","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}

            data: [DONE]


            """);

        ChatResponseVendorPerplexityExtensions? ext = chunks.Select(x => x.VendorExtensions?.Perplexity).FirstOrDefault(x => x is not null);

        Assert.That(ext, Is.Not.Null);
        Assert.That(ext!.SearchResults!.Select(x => x.Url), Is.EqualTo(new[] { "https://example.com" }));
        Assert.That(ext.RelatedQuestions, Is.EqualTo(new[] { "What next?" }));
    }

    [Test]
    public async Task InboundStream_ChunkWithoutUsageDoesNotThrow()
    {
        List<ChatResult> chunks = await Stream("""
            data: {"id":"p1","object":"chat.completion","created":1,"model":"sonar","choices":[{"index":0,"delta":{"content":"Hi"}}]}

            data: {"id":"p1","object":"chat.completion","created":1,"model":"sonar","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: [DONE]


            """);

        Assert.That(chunks, Is.Not.Empty);
    }

    private static async Task<List<ChatResult>> Stream(string sse)
    {
        OpenAiEndpointProvider provider = new OpenAiEndpointProvider(LLmProviders.Perplexity);
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
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
