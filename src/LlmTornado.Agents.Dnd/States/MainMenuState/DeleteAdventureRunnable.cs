using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
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
        string[] selectableAdventures = Directory.GetDirectories(Program.GeneratedAdventuresFilePath);

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
            string adventureName = adventurePath.Replace(Program.GeneratedAdventuresFilePath + Path.DirectorySeparatorChar, "");
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
                    string adventureName = selectedAdventurePath.Replace(Program.GeneratedAdventuresFilePath + Path.DirectorySeparatorChar, "");

                    // Confirm deletion
                    Console.WriteLine($"\n??  WARNING: This will permanently delete the adventure '{adventureName}'");
                    Console.Write("Are you sure? (y/n): ");
                    string? confirm = Console.ReadLine();

                    if (confirm?.ToLower() == "y")
                    {
                        try
                        {
                            Directory.Delete(selectedAdventurePath, recursive: true);
                            Console.WriteLine($"\n? Successfully deleted adventure: {adventureName}");
                            return ValueTask.FromResult(true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\n? Error deleting adventure: {ex.Message}");
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
