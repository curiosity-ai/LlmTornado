using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Dnd.Agents.Runnables;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;

internal class PlayerTurnRunnable : OrchestrationRunnable<FantasyDMResult, string>
{
    private readonly FantasyWorldState _gameState;

    public PlayerTurnRunnable(FantasyWorldState state, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _gameState = state;
    }

    public override ValueTask<string> Invoke(RunnableProcess<FantasyDMResult, string> input)
    {
        FantasyDMResult dMResult = input.Input;
        // Get player action
        Console.ForegroundColor = ConsoleColor.White;
        
        Console.Write("\n[Dungeon Master]:\n\n");
        Console.Write(input.Input.Narration);
        Console.Out.Flush(); // Force the buffered output to be displayed immediately
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("\n\n---- What will you do next? ----\n\n");
        Console.WriteLine(@$"
Info Log:
Scene Complete: {dMResult.SceneCompletionPercentage}%
Current Location: {dMResult.CurrentLocation}
Current Act: {dMResult.CurrentAct}
Current Scene: {dMResult.CurrentScene}
Current Scene Turn: {_gameState.CurrentSceneTurns}
Available Actions:
");
        Console.Out.Flush(); // Force the buffered output to be displayed immediately
        foreach (var action in input.Input.NextActions)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"- {action.Action}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"     Success Rate: {action.MinimumSuccessThreshold}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"     Success Outcome: {action.SuccessOutcome}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"     Failure Outcome: {action.FailureOutcome}");
            Console.Out.Flush(); // Force the buffered output to be displayed immediately
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"\n[Player]:");
        Console.Out.Flush(); // Force the buffered output to be displayed immediately
        string? result = Console.ReadLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" \n  ");
        return ValueTask.FromResult(
            result
        );
    }

}
