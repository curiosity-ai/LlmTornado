using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleStreamMissingUsageTests
{
    [Test]
    public async Task InboundStream_ChunkWithoutUsageMetadataKeepsTheEarlierUsage()
    {
        const string json = """
            [
              {"candidates":[{"content":{"role":"model","parts":[{"text":"Once upon a"}]},"index":0}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":3,"totalTokenCount":8}},
              {"candidates":[{"content":{"role":"model","parts":[{"text":" time."}]},"finishReason":"STOP","index":0}]}
            ]
            """;

        List<ChatResult> chunks = await Collect(json);

        ChatResult finish = chunks.Single(x => x.StreamInternalKind is ChatResultStreamInternalKinds.FinishData);
        Assert.That(finish.Usage, Is.Not.Null);
        Assert.That(finish.Usage!.TotalTokens, Is.EqualTo(8));
        Assert.That(finish.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.EndTurn));
    }

    private static async Task<List<ChatResult>> Collect(string json)
    {
        GoogleEndpointProvider provider = new GoogleEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
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

