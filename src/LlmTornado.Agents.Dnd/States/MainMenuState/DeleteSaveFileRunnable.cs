using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.MainMenuState;

public class DeleteSaveFileRunnable : OrchestrationRunnable<MainMenuSelection, bool>
{
    public DeleteSaveFileRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
    }

    public override ValueTask<bool> Invoke(RunnableProcess<MainMenuSelection, bool> input)
    {
        string[] selectableSaves = Directory.GetDirectories(FantasyEngineConfiguration.SavedGamesFilePath);

        if (selectableSaves.Length == 0)
        {
            Console.WriteLine("No saved games found.");
            return ValueTask.FromResult(false);
        }

        // Display available save files
        Console.WriteLine("\n" + new string('?', 80));
        Console.WriteLine("Delete Save File:");
        Console.WriteLine(new string('?', 80));
        int index = 1;

        foreach (var savePath in selectableSaves)
        {
            string saveName = savePath.Replace(FantasyEngineConfiguration.SavedGamesFilePath + Path.DirectorySeparatorChar, "");
            Console.WriteLine($"[{index}] - {saveName}");
            index++;
        }

        Console.WriteLine($"[0] - Cancel");
        Console.Write("\nSelect a save file to delete (enter number): ");
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

                if (selectedIndex >= 1 && selectedIndex <= selectableSaves.Length)
                {
                    string selectedSavePath = selectableSaves[selectedIndex - 1];
                    string saveName = selectedSavePath.Replace(FantasyEngineConfiguration.SavedGamesFilePath + Path.DirectorySeparatorChar, "");

                    // Confirm deletion
                    Console.WriteLine($"\n??  WARNING: This will permanently delete the save file '{saveName}'");
                    Console.Write("Are you sure? (y/n): ");
                    string? confirm = Console.ReadLine();

                    if (confirm?.ToLower() == "y")
                    {
                        try
                        {
                            Directory.Delete(selectedSavePath, recursive: true);
                            Console.WriteLine($"\n? Successfully deleted save file: {saveName}");
                            return ValueTask.FromResult(true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\n? Error deleting save file: {ex.Message}");
                            return ValueTask.FromResult(false);
                        }
                    }
                    else
                    {
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
}
