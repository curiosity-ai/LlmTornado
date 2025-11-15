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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.MainMenuState;

public class GenerateAdventureRunnable : OrchestrationRunnable<MainMenuSelection, bool>
{
    TornadoApi _client;
    public GenerateAdventureRunnable(TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<MainMenuSelection, bool> input)
    {
        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("🎲 Adventure Generator");
        Console.WriteLine(new string('═', 80));
        Console.WriteLine("\nThis will use AI to generate a complete adventure with:");
        Console.WriteLine("  ✨ Adventure description and difficulty");
        Console.WriteLine("  📜 Main quest line (20+ quests)");
        Console.WriteLine("  🗺️  Interconnected scenes and world map");
        Console.WriteLine("  👹 Boss encounters with scaled stats");
        Console.WriteLine("  🎯 Side quests for optional content");
        Console.WriteLine("  ⚔️  Trash mob encounters");
        Console.WriteLine("  💎 Rare events and special loot");
        Console.WriteLine("\n⚠️  Note: Generation may take several minutes and will use API credits.\n");

        Console.Write("Do you want to continue? (y/n): ");
        string? confirm = Console.ReadLine();

        if (confirm?.ToLower() != "y")
        {
            Console.WriteLine("Adventure generation cancelled.");
            return false;
        }

        Console.Write("\nOptional: Enter adventure theme (or press Enter for AI to decide): ");
        string? seed = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = null;
        }

        try
        {
            Console.WriteLine("\n🎲 Starting adventure generation...\n");
            Console.WriteLine(new string('─', 80));

            string prompt = string.IsNullOrWhiteSpace(seed) ? "Generate a fantasy adventure" : seed;
            
            // Use the generator runnable directly
            var generator = new FantasyEngine.States.GeneratorStates.AdventureGeneratorRunnable(_client, new Orchestration<bool, bool>());
            bool success = await generator.Invoke(new RunnableProcess<string, bool>(generator, prompt, "gen-id"));

            Console.WriteLine(new string('─', 80));

            if (success)
            {
                Console.WriteLine("\n✅ Adventure generated successfully!");
                Console.WriteLine("\nThe adventure has been saved to the GeneratedAdventures directory.");
                Console.WriteLine("You can now start a new game to play the generated adventure!");
            }
            else
            {
                Console.WriteLine("\n❌ Adventure generation failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error generating adventure: {ex.Message}");
            Console.WriteLine("Please check your API key and try again.");
        }
        return true;
    }
}
