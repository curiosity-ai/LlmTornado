using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.MainMenuState;

public class DeleteAdventureRunnable : OrchestrationRunnable<MainMenuSelection, bool>
{
    public DeleteAdventureRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
    }

    public override ValueTask<bool> Invoke(RunnableProcess<MainMenuSelection, bool> input)
    {
        string[] selectableAdventures = Directory.GetDirectories(FantasyEngineConfiguration.GeneratedAdventuresFilePath);

        if (selectableAdventures.Length == 0)
        {
            Console.WriteLine("No generated adventures found.");
            return ValueTask.FromResult(false);
        }

        // Display available adventures
        Console.WriteLine("\n" + new string('?', 80));
        Console.WriteLine("Delete Generated Adventure:");
        Console.WriteLine(new string('?', 80));
        int index = 1;

        foreach (var adventurePath in selectableAdventures)
        {
            string adventureName = adventurePath.Replace(FantasyEngineConfiguration.GeneratedAdventuresFilePath + Path.DirectorySeparatorChar, "");
            Console.WriteLine($"[{index}] - {adventureName}");
            index++;
        }

        Console.WriteLine($"[0] - Cancel");
        Console.Write("\nSelect an adventure to delete (enter number): ");
        string? selectionInput = Console.ReadLine();

        if (selectionInput != null)
        {
            if (int.TryParse(selectionInput, out int selectedIndex))
            {
                if (selectedIndex == 0)
                {
                    Console.WriteLine("Delete cancelled.");
                    return ValueTask.FromResult(false);
                }

                if (selectedIndex >= 1 && selectedIndex <= selectableAdventures.Length)
                {
                    string selectedAdventurePath = selectableAdventures[selectedIndex - 1];
                    string adventureName = selectedAdventurePath.Replace(FantasyEngineConfiguration.GeneratedAdventuresFilePath + Path.DirectorySeparatorChar, "");

                    Console.WriteLine($"\nSelected adventure: {adventureName}");
                    Console.WriteLine("What would you like to delete?");
                    Console.WriteLine("  1. Delete entire adventure (all revisions)");
                    Console.WriteLine("  2. Delete a specific revision");
                    Console.WriteLine("  0. Cancel");
                    Console.Write("Choice: ");
                    var deleteMode = Console.ReadLine();

                    switch (deleteMode)
                    {
                        case "1":
                            return ValueTask.FromResult(DeleteEntireAdventure(selectedAdventurePath, adventureName));
                        case "2":
                            return ValueTask.FromResult(DeleteRevision(selectedAdventurePath, adventureName));
                        default:
                            Console.WriteLine("Delete cancelled.");
                            return ValueTask.FromResult(false);
                    }
                }
                else
                {
                    Console.WriteLine("Invalid selection.");
                    return ValueTask.FromResult(false);
                }
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter a number.");
                return ValueTask.FromResult(false);
            }
        }
        else
        {
            Console.WriteLine("No input received.");
            return ValueTask.FromResult(false);
        }
    }
    private static bool DeleteEntireAdventure(string adventurePath, string adventureName)
    {
        Console.WriteLine($"\n??  WARNING: This will permanently delete the adventure '{adventureName}' and all revisions.");
        Console.Write("Are you sure? (y/n): ");
        string? confirm = Console.ReadLine();

        if (confirm?.ToLower() != "y")
        {
            Console.WriteLine("Delete cancelled.");
            return false;
        }

        try
        {
            Directory.Delete(adventurePath, recursive: true);
            Console.WriteLine($"\n? Successfully deleted adventure: {adventureName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n? Error deleting adventure: {ex.Message}");
            return false;
        }
    }

    private static bool DeleteRevision(string adventurePath, string adventureName)
    {
        var manifest = AdventureRevisionManager.EnsureManifest(adventurePath, adventureName);

        if (manifest.Revisions.Count == 0)
        {
            Console.WriteLine("This adventure has no revisions to delete.");
            return false;
        }

        Console.WriteLine("\nAvailable revisions:");
        for (var index = 0; index < manifest.Revisions.Count; index++)
        {
            var entry = manifest.Revisions[index];
            Console.WriteLine($"  [{index + 1}] {entry.Label} ({entry.RevisionId}) - {entry.CreatedAtUtc.ToLocalTime():g}");
        }
        Console.WriteLine("  [a] Delete all revisions (entire adventure)");
        Console.WriteLine("  [0] Cancel");
        Console.Write("Select revision to delete: ");
        var selection = Console.ReadLine();

        if (string.Equals(selection, "0", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Delete cancelled.");
            return false;
        }

        if (string.Equals(selection, "a", StringComparison.OrdinalIgnoreCase))
        {
            return DeleteEntireAdventure(adventurePath, adventureName);
        }

        if (!int.TryParse(selection, out var revisionIndex) ||
            revisionIndex < 1 ||
            revisionIndex > manifest.Revisions.Count)
        {
            Console.WriteLine("Invalid revision selection.");
            return false;
        }

        var revision = manifest.Revisions[revisionIndex - 1];
        Console.Write($"Confirm delete of revision '{revision.Label}' ({revision.RevisionId})? (y/n): ");
        var confirm = Console.ReadLine();
        if (confirm?.ToLower() != "y")
        {
            Console.WriteLine("Delete cancelled.");
            return false;
        }

        var deleted = AdventureRevisionManager.DeleteRevision(adventurePath, manifest, revision.RevisionId);
        if (!deleted)
        {
            Console.WriteLine("Failed to delete revision. It may have already been removed.");
            return false;
        }

        Console.WriteLine($"Revision '{revision.RevisionId}' deleted.");

        if (manifest.Revisions.Count == 0)
        {
            Console.WriteLine("No revisions remain for this adventure. Removing adventure folder.");
            try
            {
                Directory.Delete(adventurePath, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to remove empty adventure folder: {ex.Message}");
            }
        }

        return true;
    }
}
