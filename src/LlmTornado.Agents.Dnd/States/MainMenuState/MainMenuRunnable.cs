using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Persistence;
using LlmTornado.Chat;
using LlmTornado.Code;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.Agents.FantasyEngine;

public class MainMenuRunnable : OrchestrationRunnable<ChatMessage, MainMenuSelection>
{
    private static TornadoApi? _client;

    public MainMenuRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
    }

    public override async  ValueTask<MainMenuSelection> Invoke(RunnableProcess<ChatMessage, MainMenuSelection> input)
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        LlmTornado D&D - AI-Powered Dungeon & Dragons Adventure        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("\n" + new string('═', 80));
            Console.WriteLine("Main Menu:");
            Console.WriteLine("  1. Start New Adventure");
            Console.WriteLine("  2. Load Saved Game");
            Console.WriteLine("  3. Generate New Adventure");
            Console.WriteLine("  4. Exit");
            Console.WriteLine(new string('═', 80));
            Console.Write("Select option: ");

            string? choice = Console.ReadLine();

            var selection =  choice switch
            {
                "1" => MainMenuSelection.StartNewAdventure,
                "2" => MainMenuSelection.LoadSavedGame,
                "3" => MainMenuSelection.GenerateNewAdventure,
                "4" => MainMenuSelection.ExitGame,
                _ => MainMenuSelection.InvalidSelection
            };

            if(selection == MainMenuSelection.InvalidSelection)
            {
                Console.WriteLine("Invalid selection. Please try again.");
                continue;
            }
            return selection;
        }

    }
}
