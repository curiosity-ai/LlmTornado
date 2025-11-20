using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd;
using LlmTornado.Agents.Dnd.FantasyEngine;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Game;
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

    public MainMenuRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
    }


    public override async  ValueTask<MainMenuSelection> Invoke(RunnableProcess<ChatMessage, MainMenuSelection> input)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;

        while (true)
        {
            Console.WriteLine("\n" + new string('═', 80));
            Console.WriteLine("Main Menu:");
            Console.WriteLine("  1. Start New Adventure");
            Console.WriteLine("  2. Load Saved Game");
            Console.WriteLine("  3. Generate New Adventure");
            Console.WriteLine("  4. Edit Generated Adventure");
            Console.WriteLine("  5. Delete Generated Adventure");
            Console.WriteLine("  6. Delete Save File");
            Console.WriteLine($"  7. Settings (Narration TTS: {(FantasyEngineConfiguration.Settings.EnableTts ? "ON" : "OFF")})");
            Console.WriteLine("  8. Quit");
            Console.WriteLine(new string('═', 80));
            Console.Write("Select option: ");

            Console.Out.Flush(); // Force the buffered output to be displayed immediately

            string? choice = Console.ReadLine();

            var selection =  choice switch
            {
                "1" => MainMenuSelection.StartNewAdventure,
                "2" => MainMenuSelection.LoadSavedGame,
                "3" => MainMenuSelection.GenerateNewAdventure,
                "4" => MainMenuSelection.EditGeneratedAdventure,
                "5" => MainMenuSelection.DeleteAdventure,
                "6" => MainMenuSelection.DeleteSaveFile,
                "7" => MainMenuSelection.Settings,
                "8" => MainMenuSelection.QuitGame,
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
