using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.Agents.Runnables.FantasyEngine;

public class StartNewGameRunnable : OrchestrationRunnable<MainMenuSelection, bool>
{
    public StartNewGameRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {

    }

    public override ValueTask<bool> Invoke(RunnableProcess<MainMenuSelection, bool> input)
    {
        string[] selectableAdventures = Directory.GetDirectories(Path.Combine(Directory.GetCurrentDirectory(), "GeneratedAdventures"));

        //Selector for which adventure to load
        Console.WriteLine("Available Adventures:");
        int index = 1;

        foreach (var adventurePath in selectableAdventures)
        {
            string adventureName = adventurePath.Replace(Path.Combine(Directory.GetCurrentDirectory(), "GeneratedAdventures") + Path.DirectorySeparatorChar, "");
            Console.WriteLine($"[{index}] - {adventureName}");
            index++;
        }

        Console.Write("Select an adventure to start (enter number): ");
        string? selectionInput = Console.ReadLine();

        if (selectionInput != null) {
            if (int.TryParse(selectionInput, out int selectedIndex))
            {
                if (selectedIndex >= 1 && selectedIndex <= selectableAdventures.Length)
                {
                    string selectedAdventurePath = selectableAdventures[selectedIndex - 1];
                    string adventureFile = Path.Combine(selectedAdventurePath, "adventure.json");

                    // Load the adventure result from the generated adventure
                    FantasyAdventureResult adventureResult = new();
                    adventureResult = adventureResult.DeserializeFromFile(adventureFile);

                    // Create session directory
                    string saveDataDir = Path.Combine(Directory.GetCurrentDirectory(), "GameSaveData");
                    string sessionDir = Path.Combine(saveDataDir, $"{adventureResult.Title.Replace(" ", "_").Replace(":", "_")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");

                    if(!Directory.Exists(sessionDir))
                    {
                        Directory.CreateDirectory(sessionDir);
                    }

                    // Create world state with SaveDataDirectory set
                    FantasyWorldState worldState = new FantasyWorldState();
                    worldState.SaveDataDirectory = sessionDir;
                    worldState.AdventureResult = adventureResult;
                    worldState.Adventure = adventureResult.ToFantasyAdventure();

                    if (worldState.CurrentLocation is null)
                    {
                        worldState.CurrentLocation = worldState.Adventure.Locations.FirstOrDefault(location => worldState.Adventure.PlayerStartingInfo.StartingLocationId == location.Id) ?? new FantasyLocation("Unknown", "Unknown", "unknown");
                    }

                    Console.WriteLine($"Loaded adventure: {worldState.Adventure.Title}");
                    
                    // Save state using the helper property
                    worldState.SerializeToFile(worldState.WorldStateFile);
                    Console.WriteLine($"Created world state file: {worldState.WorldStateFile}");
                    
                    // Here you would typically set this world state into a global context or pass it to the game engine
                    // For this example, we just print confirmation
                    Console.WriteLine($"Adventure '{worldState.Adventure.Title}' loaded successfully!");
                }
                else
                {
                    Console.WriteLine("Invalid selection. Please restart and choose a valid adventure number.");
                }
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter a number corresponding to the adventure.");
            }
        }
        else
        {
            Console.WriteLine("No input received. Please restart and select an adventure.");
        }

        return ValueTask.FromResult(true);
    }
}
