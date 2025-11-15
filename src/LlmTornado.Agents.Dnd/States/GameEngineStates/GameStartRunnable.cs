using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Dnd.Agents.Runnables;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;

internal class GameStartRunnable : OrchestrationRunnable<ChatMessage, string>
{
    private readonly FantasyWorldState _gameState;

    public GameStartRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {

    }

    public override ValueTask<string> Invoke(RunnableProcess<ChatMessage, string> input)
    {
        // Get player action
        return ValueTask.FromResult(
            input.Input.GetMessageContent()
        );
    }

}
