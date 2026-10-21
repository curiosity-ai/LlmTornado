using System.Reflection;
using LlmTornado.Agents;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Tests;

[TestFixture]
public class TornadoRunnerResponseIdTests
{
    [Test]
    public void SetupConversation_WithResponseId_SetsThePreviousResponseId()
    {
        TornadoAgent agent = new TornadoAgent(new TornadoApi(LLmProviders.OpenAi, "key"), ChatModel.OpenAi.Gpt4.O);

        Conversation conversation = Setup(agent, "resp_123");

        Assert.That(conversation.RequestParameters.ResponseRequestParameters?.PreviousResponseId, Is.EqualTo("resp_123"));
    }

    [Test]
    public void SetupConversation_WithoutResponseId_LeavesTheResponseParametersAlone()
    {
        TornadoAgent agent = new TornadoAgent(new TornadoApi(LLmProviders.OpenAi, "key"), ChatModel.OpenAi.Gpt4.O);

        Conversation conversation = Setup(agent, null);

        Assert.That(conversation.RequestParameters.ResponseRequestParameters, Is.Null);
    }

    private static Conversation Setup(TornadoAgent agent, string? responseId)
    {
        MethodInfo method = typeof(TornadoRunner).GetMethod("SetupConversation", BindingFlags.NonPublic | BindingFlags.Static)!;

        try
        {
            return (Conversation)method.Invoke(null, [agent, new List<ChatMessagePart> { new ChatMessagePart("hi") }, null, responseId, null, CancellationToken.None])!;
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }
}
