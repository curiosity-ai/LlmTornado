using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Chat;
using LlmTornado.Code;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

internal class RunGameRunnable : OrchestrationRunnable<string, ChatMessage>
{
    public TornadoApi _client;
    public RunGameRunnable(TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
    }

    public override async ValueTask<ChatMessage> Invoke(RunnableProcess<string, ChatMessage> input)
    {
        Console.Clear();

        ChatRuntime.ChatRuntime runtime = new ChatRuntime.ChatRuntime(new FantasyEngineConfiguration(_client, Program.WorldState));

        await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, input.Input));

        return new ChatMessage(Code.ChatMessageRoles.Assistant, "Player has quit the game.");
    }
}
