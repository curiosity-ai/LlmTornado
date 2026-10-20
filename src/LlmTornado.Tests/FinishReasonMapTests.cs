using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class FinishReasonMapTests
{
    [TestCase("STOP", ChatMessageFinishReasons.EndTurn)]
    [TestCase("MAX_TOKENS", ChatMessageFinishReasons.Length)]
    [TestCase("SAFETY", ChatMessageFinishReasons.ContentFilter)]
    [TestCase("MALFORMED_FUNCTION_CALL", ChatMessageFinishReasons.MalformedToolCall)]
    [TestCase("stop", ChatMessageFinishReasons.EndTurn)]
    [TestCase("tool_use", ChatMessageFinishReasons.ToolCalls)]
    public void Map_ResolvesProviderFinishReasonsRegardlessOfCase(string reason, ChatMessageFinishReasons expected)
    {
        Assert.That(ChatMessageFinishReasonsConverter.Map.GetValueOrDefault(reason, ChatMessageFinishReasons.Unknown), Is.EqualTo(expected));
    }

    [Test]
    public async Task GoogleInboundStream_ReportsTheUppercaseFinishReasonGeminiSends()
    {
        const string json = """
            [
              {"candidates":[{"content":{"role":"model","parts":[{"text":"Hi"}]},"finishReason":"MAX_TOKENS","index":0}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":1,"totalTokenCount":6}}
            ]
            """;

        GoogleEndpointProvider provider = new GoogleEndpointProvider();
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        ChatRequest request = new ChatRequest
        {
            Stream = true,
            Messages = [new ChatMessage(ChatMessageRoles.User, "test")]
        };

        ChatResult? finish = null;
        await foreach (ChatResult? chunk in provider.InboundStream(reader, request, null))
        {
            if (chunk?.StreamInternalKind is ChatResultStreamInternalKinds.FinishData)
            {
                finish = chunk;
            }
        }

        Assert.That(finish, Is.Not.Null);
        Assert.That(finish!.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.Length));
    }
}
