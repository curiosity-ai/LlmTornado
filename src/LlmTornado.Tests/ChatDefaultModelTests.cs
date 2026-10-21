using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class ChatDefaultModelTests
{
    [Test]
    public void ChatRequest_DefaultModelIsNotTheRetiredGpt35Turbo()
    {
        ChatRequest request = new ChatRequest();

        Assert.That(request.Model?.Name, Is.EqualTo(ChatModel.OpenAi.Gpt56.V56Terra.Name));
    }

    [Test]
    public void ChatRequest_DefaultsLeaveSamplingAndTokenLimitsUnset()
    {
        ChatRequest request = new ChatRequest
        {
            Messages = [new ChatMessage(ChatMessageRoles.User, "hi")]
        };

        string json = JsonConvert.SerializeObject(request);

        Assert.That(json, Does.Contain("\"model\":\"gpt-5.6-terra\""));
        Assert.That(json, Does.Contain("\"temperature\":null"));
        Assert.That(json, Does.Contain("\"top_p\":null"));
        Assert.That(json, Does.Contain("\"max_tokens\":null"));
    }
}
