using System.Reflection;
using LlmTornado.Agents;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Tests;

[TestFixture]
public class TornadoAgentCancelTests
{
    [Test]
    public void Cancel_WhileRunning_ReturnsTrueAndMarksTheRunCancelled()
    {
        TornadoAgent agent = CreateAgent(running: true);

        bool cancelled = agent.Cancel();

        Assert.That(cancelled, Is.True);
        Assert.That(agent.Cancelled, Is.True);
    }

    [Test]
    public void Cancel_CalledAgainWhileRunning_StillReportsTheRunAsCancelled()
    {
        TornadoAgent agent = CreateAgent(running: true);

        agent.Cancel();

        Assert.That(agent.Cancel(), Is.True);
    }

    [Test]
    public void Cancel_WhenNothingIsRunning_ReturnsFalse()
    {
        TornadoAgent agent = CreateAgent(running: false);

        Assert.That(agent.Cancel(), Is.False);
        Assert.That(agent.Cancelled, Is.False);
    }

    private static TornadoAgent CreateAgent(bool running)
    {
        TornadoAgent agent = new TornadoAgent(new TornadoApi(LLmProviders.OpenAi, "key"), ChatModel.OpenAi.Gpt4.O);
        typeof(TornadoAgent).GetProperty(nameof(TornadoAgent.Running))!.SetValue(agent, running);
        return agent;
    }
}
