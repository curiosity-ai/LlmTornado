using System.IO;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleToolCallFinishReasonTests
{
    [Test]
    public void ToChatResult_StopWithFunctionCallReportsToolCalls()
    {
        const string json = """
            {"candidates":[{"content":{"role":"model","parts":[{"functionCall":{"name":"get_weather","args":{"city":"Prague"}}}]},"finishReason":"STOP","index":0}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":3,"totalTokenCount":8}}
            """;

        VendorGoogleChatResult? result = JsonConvert.DeserializeObject<VendorGoogleChatResult>(json);
        ChatResult chatResult = result!.ToChatResult(null, new ChatRequest { Messages = [new ChatMessage(ChatMessageRoles.User, "test")] });

        Assert.That(chatResult.Choices![0].Message?.ToolCalls, Has.Count.EqualTo(1));
        Assert.That(chatResult.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.ToolCalls));
    }

    [Test]
    public void ToChatResult_StopWithoutFunctionCallStaysEndTurn()
    {
        const string json = """
            {"candidates":[{"content":{"role":"model","parts":[{"text":"Sunny."}]},"finishReason":"STOP","index":0}]}
            """;

        VendorGoogleChatResult? result = JsonConvert.DeserializeObject<VendorGoogleChatResult>(json);
        ChatResult chatResult = result!.ToChatResult(null, new ChatRequest { Messages = [new ChatMessage(ChatMessageRoles.User, "test")] });

        Assert.That(chatResult.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.EndTurn));
    }

    [Test]
    public void ToChatResult_MaxTokensWithFunctionCallStaysLength()
    {
        const string json = """
            {"candidates":[{"content":{"role":"model","parts":[{"functionCall":{"name":"get_weather","args":{}}}]},"finishReason":"MAX_TOKENS","index":0}]}
            """;

        VendorGoogleChatResult? result = JsonConvert.DeserializeObject<VendorGoogleChatResult>(json);
        ChatResult chatResult = result!.ToChatResult(null, new ChatRequest { Messages = [new ChatMessage(ChatMessageRoles.User, "test")] });

        Assert.That(chatResult.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.Length));
    }

    [Test]
    public async Task InboundStream_StopWithFunctionCallReportsToolCalls()
    {
        const string json = """
            [
              {"candidates":[{"content":{"role":"model","parts":[{"functionCall":{"name":"get_weather","args":{"city":"Prague"}}}]},"finishReason":"STOP","index":0}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":3,"totalTokenCount":8}}
            ]
            """;

        List<ChatResult> chunks = await Collect(json);

        ChatResult finish = chunks.Single(x => x.StreamInternalKind is ChatResultStreamInternalKinds.FinishData);
        Assert.That(finish.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.ToolCalls));
    }

    [Test]
    public async Task InboundStream_StopWithoutFunctionCallStaysEndTurn()
    {
        const string json = """
            [
              {"candidates":[{"content":{"role":"model","parts":[{"text":"Sunny."}]},"finishReason":"STOP","index":0}]}
            ]
            """;

        List<ChatResult> chunks = await Collect(json);

        ChatResult finish = chunks.Single(x => x.StreamInternalKind is ChatResultStreamInternalKinds.FinishData);
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
