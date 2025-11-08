using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Agents.Dnd.Persistence;
using LlmTornado.Chat;
using LlmTornado.Code;
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

        Console.Write("\nOptional: Enter adventure theme/seed (or press Enter for AI to decide): ");
        string? seed = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = null;
        }

        try
        {
            var persistence = new AdventurePersistence();
            var config = new AdventureGeneratorConfiguration(_client!, persistence, seed);
            var runtime = new ChatRuntime.ChatRuntime(config);

            Console.WriteLine("\n🎲 Starting adventure generation...\n");
            Console.WriteLine(new string('─', 80));

            var result = await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, "Generate adventure"));

            Console.WriteLine(new string('─', 80));

            if (config.Adventure != null && !string.IsNullOrEmpty(config.Adventure.Id))
            {
                Console.WriteLine("\n✅ Adventure generated successfully!");
                Console.WriteLine($"\n📖 Adventure: {config.Adventure.Name}");
                Console.WriteLine($"📝 Description: {config.Adventure.Description}");
                Console.WriteLine($"⚡ Difficulty: {config.Adventure.Difficulty}");
                Console.WriteLine($"🗺️  Scenes: {config.Adventure.Scenes.Count}");
                Console.WriteLine($"📜 Main Quests: {config.Adventure.MainQuestLine.Count}");
                Console.WriteLine($"🎯 Side Quests: {config.Adventure.SideQuests.Count}");
                Console.WriteLine($"👹 Bosses: {config.Adventure.Bosses.Count}");
                Console.WriteLine($"💎 Rare Events: {config.Adventure.RareEvents.Count}");
                Console.WriteLine($"\n💾 Adventure ID: {config.Adventure.Id}");
                Console.WriteLine("\nYou can now start a new game and reference this adventure!");
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
