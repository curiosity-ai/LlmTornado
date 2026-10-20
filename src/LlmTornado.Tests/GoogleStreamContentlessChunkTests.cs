using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleStreamContentlessChunkTests
{
    [Test]
    public async Task InboundStream_FinalChunkWithoutContentKeepsTheUsage()
    {
        // A MAX_TOKENS spent on thinking or a SAFETY block ends the stream with a candidate that has no content.
        const string json = """
            [
              {"candidates":[{"content":{"role":"model","parts":[{"text":"Once upon a"}]},"index":0}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":3,"totalTokenCount":8}},
              {"candidates":[{"finishReason":"MAX_TOKENS","index":0}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":3,"thoughtsTokenCount":40,"totalTokenCount":48}}
            ]
            """;

        List<ChatResult> chunks = await Collect(json);

        ChatResult finish = chunks.Single(x => x.StreamInternalKind is ChatResultStreamInternalKinds.FinishData);
        Assert.That(finish.Usage, Is.Not.Null);
        Assert.That(finish.Usage!.TotalTokens, Is.EqualTo(48));
    }

    [Test]
    public void ToChatResult_CandidateWithoutContentIsAnEmptyAssistantMessage()
    {
        const string json = """
            {"candidates":[{"finishReason":"SAFETY","index":0}],"usageMetadata":{"promptTokenCount":5,"totalTokenCount":5}}
            """;

        VendorGoogleChatResult? result = JsonConvert.DeserializeObject<VendorGoogleChatResult>(json);
        ChatResult chatResult = result!.ToChatResult(null, new ChatRequest { Messages = [new ChatMessage(ChatMessageRoles.User, "test")] });

        Assert.That(chatResult.Choices, Has.Count.EqualTo(1));
        Assert.That(chatResult.Choices![0].Message?.Content, Is.Empty);
        Assert.That(chatResult.Usage!.TotalTokens, Is.EqualTo(5));
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

