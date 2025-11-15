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
    int ConsoleMarginWidth = 25;
    int maxConsoleWidth = 200;
    public PlayerTurnRunnable(FantasyWorldState state, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _gameState = state;
    }

    public override ValueTask<string> Invoke(RunnableProcess<FantasyDMResult, string> input)
    {
        maxConsoleWidth = Console.WindowWidth - ConsoleMarginWidth;
        FantasyDMResult dMResult = input.Input;
        // Get player action
        Console.ForegroundColor = ConsoleColor.White;
        
        Console.Write("\n[Dungeon Master]:\n\n");
        WrapText(input.Input.Narration, maxConsoleWidth);
        Console.Out.Flush(); // Force the buffered output to be displayed immediately
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("\n\n---- What will you do next? ----\n\n");
        WrapText(@$"

World Info:
# {_gameState.Adventure.Title} 
- Act {_gameState.CurrentAct + 1}: {_gameState.Adventure.Acts[_gameState.CurrentAct].Title} 
- Scene {_gameState.CurrentScene + 1}: {_gameState.Adventure.Acts[_gameState.CurrentAct].Scenes[_gameState.CurrentScene]}
- Location {_gameState.CurrentLocation}

DM Info:
Scene Complete: {dMResult.SceneCompletionPercentage}%

Current Scene Turn: {_gameState.CurrentSceneTurns}
Available Actions:
", maxConsoleWidth);
        Console.Out.Flush(); // Force the buffered output to be displayed immediately
        foreach (var action in input.Input.NextActions)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            WrapText($"- {action.Action}", maxConsoleWidth);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("     Success Rate:");
            WrapText($"{action.MinimumSuccessThreshold}", maxConsoleWidth);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("     Success Outcome: ");
            WrapText($"{action.SuccessOutcome}", maxConsoleWidth);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("     Failure Outcome: ");
            WrapText($"{action.FailureOutcome}", maxConsoleWidth);
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

    public void WrapText(string text, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string[] words = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder currentLine = new StringBuilder();

        foreach (string word in words)
        {
            // If adding this word would exceed maxWidth
            if (currentLine.Length > 0 && currentLine.Length + 1 + word.Length > maxWidth)
            {
                // Output current line and start fresh
                Console.WriteLine(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(word);
            }
            else
            {
                // Add word to current line
                if (currentLine.Length > 0)
                {
                    currentLine.Append(" ");
                }
                currentLine.Append(word);
            }
        }

        // Output any remaining text
        if (currentLine.Length > 0)
        {
            Console.WriteLine(currentLine.ToString());
        }
    }
}
