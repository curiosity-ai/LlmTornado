using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

public class LoadGameRunnable : OrchestrationRunnable<MainMenuSelection, bool>
{
    public LoadGameRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {

    }

    public override ValueTask<bool> Invoke(RunnableProcess<MainMenuSelection, bool> input)
    {
        string[] selectableAdventures = Directory.GetDirectories(FantasyEngineConfiguration.SavedGamesFilePath);

        if(selectableAdventures.Length == 0)
        {
            Console.WriteLine("No saved adventures found. Please Start a new game.");
            return ValueTask.FromResult(false);
        }

        //Selector for which adventure to load
        Console.WriteLine("Load Save:");
        int index = 1;

        foreach (var adventurePath in selectableAdventures)
        {
            string adventureName = adventurePath.Replace(FantasyEngineConfiguration.SavedGamesFilePath + Path.DirectorySeparatorChar, "");
            Console.WriteLine($"[{index}] - {adventureName}");
            index++;
        }

        Console.Write("Select an adventure to start (enter number): ");
        string? selectionInput = Console.ReadLine();

        if (selectionInput != null)
        {
            if (int.TryParse(selectionInput, out int selectedIndex))
            {
                if (selectedIndex >= 1 && selectedIndex <= selectableAdventures.Length)
                {
                    string selectedAdventurePath = selectableAdventures[selectedIndex - 1];
                    
                    // Load world state from save directory
                    string stateFile = Path.Combine(selectedAdventurePath, "state.json");
                    
                    if (!File.Exists(stateFile))
                    {
                        Console.WriteLine($"Save file not found: {stateFile}");
                        return ValueTask.FromResult(false);
                    }

                    FantasyEngineConfiguration.WorldState = FantasyWorldState.DeserializeFromFile(stateFile);
                    FantasyEngineConfiguration.WorldState.EnableTts = FantasyEngineConfiguration.Settings.EnableTts;

                    // Ensure SaveDataDirectory is set correctly
                    FantasyEngineConfiguration.WorldState.SaveDataDirectory = selectedAdventurePath;

                    // Verify the adventure was loaded successfully
                    if (FantasyEngineConfiguration.WorldState.Adventure == null)
                    {
                        Console.WriteLine($"Error: Failed to load adventure data from {stateFile}");
                        Console.WriteLine("The save file may be corrupted or incompatible.");
                        return ValueTask.FromResult(false);
                    }

                    var revisionId = string.IsNullOrWhiteSpace(FantasyEngineConfiguration.WorldState.AdventureRevisionId)
                        ? "rev_legacy"
                        : FantasyEngineConfiguration.WorldState.AdventureRevisionId;

                    Console.WriteLine($"Loaded adventure: {FantasyEngineConfiguration.WorldState.Adventure.Title} ({revisionId})");
                    Console.WriteLine($"Loaded world state file: {stateFile}");
                    // Here you would typically set this world state into a global context or pass it to the game engine
                    // For this example, we just print confirmation
                    Console.WriteLine($"Adventure '{FantasyEngineConfiguration.WorldState.Adventure.Title}' loaded successfully!");

                    return ValueTask.FromResult(true);
                }
                else
                {
                    Console.WriteLine("Invalid selection. Please restart and choose a valid adventure number.");
                    return ValueTask.FromResult(false);
                }
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter a number corresponding to the adventure.");
                return ValueTask.FromResult(false);
            }
        }
        else
        {
            Console.WriteLine("No input received. Please restart and select an adventure.");
            return ValueTask.FromResult(false);
        }
    }
}
