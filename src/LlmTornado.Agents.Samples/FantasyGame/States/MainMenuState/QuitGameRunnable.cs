using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

public class QuitGameRunnable : OrchestrationRunnable<MainMenuSelection, ChatMessage>
{
    public QuitGameRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
    }

    public override ValueTask<ChatMessage> Invoke(RunnableProcess<MainMenuSelection, ChatMessage> input)
    {
        Orchestrator.HasCompletedSuccessfully();
        return ValueTask.FromResult(
            new ChatMessage(Code.ChatMessageRoles.Assistant, "Player has quit the game.")
        );
    }
}
