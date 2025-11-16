using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using LlmTornado.Agents.Dnd.Utility;
using System;
using System.Collections.Generic;
using System.IO;
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
        string[] selectableAdventures = Directory.GetDirectories(Program.GeneratedAdventuresFilePath);

        if (selectableAdventures.Length == 0)
        {
            Console.WriteLine("No adventures available to load. Please create a new adventure first.");
            return ValueTask.FromResult(false);
        }

        //Selector for which adventure to load
        Console.WriteLine("Available Adventures:");
        int index = 1;

        foreach (var adventurePath in selectableAdventures)
        {
            string adventureName = adventurePath.Replace(Program.GeneratedAdventuresFilePath + Path.DirectorySeparatorChar, "");
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
                    string adventureName = Path.GetFileName(selectedAdventurePath) ?? "Adventure";

                    var manifest = AdventureRevisionManager.EnsureManifest(selectedAdventurePath, adventureName);
                    var revisions = manifest.Revisions
                        .OrderByDescending(r => r.CreatedAtUtc)
                        .ToList();

                    if (revisions.Count == 0)
                    {
                        Console.WriteLine("No revisions available for this adventure.");
                        return ValueTask.FromResult(false);
                    }

                    Console.WriteLine("\nAvailable revisions:");
                    for (var revisionIndex = 0; revisionIndex < revisions.Count; revisionIndex++)
                    {
                        var revision = revisions[revisionIndex];
                        Console.WriteLine($"  [{revisionIndex + 1}] {revision.Label} ({revision.RevisionId}) - {revision.CreatedAtUtc.ToLocalTime():g}");
                    }

                    Console.Write($"Select a revision to load (default {revisions[0].RevisionId}): ");
                    var revisionSelection = Console.ReadLine();

                    AdventureRevisionEntry chosenRevision;
                    if (string.IsNullOrWhiteSpace(revisionSelection))
                    {
                        chosenRevision = revisions[0];
                    }
                    else if (int.TryParse(revisionSelection, out int revisionChoice) &&
                             revisionChoice >= 1 &&
                             revisionChoice <= revisions.Count)
                    {
                        chosenRevision = revisions[revisionChoice - 1];
                    }
                    else
                    {
                        Console.WriteLine("Invalid revision selection.");
                        return ValueTask.FromResult(false);
                    }

                    string revisionPath = AdventureRevisionManager.GetRevisionPath(selectedAdventurePath, chosenRevision.RevisionId);
                    string adventureFile = Path.Combine(revisionPath, "adventure.json");

                    // Load the adventure result from the generated adventure
                    FantasyAdventureResult adventureResult = new();
                    adventureResult = adventureResult.DeserializeFromFile(adventureFile);

                    // Create session directory
                    string sessionDir = Path.Combine(Program.SavedGamesFilePath, $"{adventureResult.Title.Replace(" ", "_").Replace(":", "_")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");

                    if(!Directory.Exists(sessionDir))
                    {
                        Directory.CreateDirectory(sessionDir);
                    }

                    // Create world state with SaveDataDirectory set
                    Program.WorldState = new FantasyWorldState();
                    Program.WorldState.SaveDataDirectory = sessionDir;
                    Program.WorldState.Adventure = adventureResult.ToFantasyAdventure();
                    Program.WorldState.AdventureRevisionId = chosenRevision.RevisionId;

                    if (Program.WorldState.CurrentLocation is null)
                    {
                        Program.WorldState.CurrentLocation = Program.WorldState.Adventure.Locations.FirstOrDefault(location => Program.WorldState.Adventure.PlayerStartingInfo.StartingLocationId == location.Id) ?? new FantasyLocation("Unknown", "Unknown", "unknown", false);
                    }

                    if (!File.Exists(Program.WorldState.MemoryFile))
                    {
                        string? dir = Path.GetDirectoryName(Program.WorldState.MemoryFile);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        File.WriteAllText(Program.WorldState.MemoryFile, "# Objectives\n\n");
                    }

                    if (!File.Exists(Program.WorldState.CompletedObjectivesFile))
                    {
                        string? dir = Path.GetDirectoryName(Program.WorldState.CompletedObjectivesFile);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.WriteAllText(Program.WorldState.CompletedObjectivesFile, "# Completed Objectives Log\n\n");
                    }

                    Console.WriteLine($"Loaded adventure: {Program.WorldState.Adventure.Title} ({chosenRevision.RevisionId})");

                    // Save state using the helper property
                    Program.WorldState.SerializeToFile(Program.WorldState.WorldStateFile);
                    Console.WriteLine($"Created world state file: {Program.WorldState.WorldStateFile}");
                    
                    // Here you would typically set this world state into a global context or pass it to the game engine
                    // For this example, we just print confirmation
                    Console.WriteLine($"Adventure '{Program.WorldState.Adventure.Title}' loaded successfully!");
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

        return ValueTask.FromResult(true);
    }
}
