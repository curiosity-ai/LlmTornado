using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Common;
using LlmTornado.Code;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Step 3: Generate scenes for the quest line
/// </summary>
public class Step3_ScenesRunnable : OrchestrationRunnable<GenerationStepResult, GenerationStepResult>
{
    private TornadoAgent Generator;
    private Adventure Adventure;

    public Step3_ScenesRunnable(TornadoApi client, Orchestration orchestrator, Adventure adventure)
        : base(orchestrator)
    {
        Adventure = adventure;

        string instructions = """
            You are a D&D world builder. Create detailed scenes/locations for the adventure.
            Each scene should have a grid size, scale, exits connecting to other scenes, NPCs, and descriptive details.
            Create a connected world map where scenes link together logically.
            Include varied terrain types and interesting locations.
            """;

        Generator = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.O,
            name: "Scene Designer",
            instructions: instructions,
            outputSchema: typeof(SceneGeneration));
    }

    public override async ValueTask<GenerationStepResult> Invoke(RunnableProcess<GenerationStepResult, GenerationStepResult> process)
    {
        process.RegisterAgent(Generator);

        Console.WriteLine("\n🎲 Step 3/7: Generating scenes and world map...");

        var questScenes = string.Join(", ", Adventure.MainQuestLine.Select(q => $"{q.Name} (Start: {q.StartEvent})"));

        var prompt = $"""
            Generate scenes for the adventure "{Adventure.Name}".
            Setting: {Adventure.Setting}
            
            Quests that need scenes: {questScenes}
            
            Create interconnected scenes with:
            - Unique IDs for each scene
            - scales (Small for buildings, Medium for regions, Large for overworld)
            - Exits that reference other scene IDs to create a map with requirements such as keys or quest items:
                - Each scene must have at least one exit to another scene
                - There must be a path from the starting scene to every other scene
            - NPCs present in each scene
            - Terrain types appropriate to the setting
            
            Ensure all quest start and completion locations have corresponding scenes.
            """;

        var messages = new List<ChatMessage> { new ChatMessage(ChatMessageRoles.User, prompt) };
        var conv = await Generator.Run(appendMessages: messages);
        SceneGeneration? result = await conv.Messages.Last().Content?.SmartParseJsonAsync<SceneGeneration>(Generator);

        if (result == null || !result.HasValue || result.Value.Scenes.Length == 0)
        {
            Console.WriteLine("❌ Failed to generate scenes");
            return new GenerationStepResult { Success = false, Message = "Failed to generate scenes" };
        }

        foreach (var scene in result.Value.Scenes)
        {
            Adventure.Scenes[scene.Id] = scene;
        }

        // Link quests to scenes
        foreach (var quest in Adventure.MainQuestLine)
        {
            if (string.IsNullOrEmpty(quest.StartSceneId) && Adventure.Scenes.Any())
            {
                quest.StartSceneId = Adventure.Scenes.First().Key;
            }
        }

        Console.WriteLine($"✅ Generated {Adventure.Scenes.Count} interconnected scenes");

        return new GenerationStepResult { Success = true, Message = "Scenes generated" };
    }
}

/// <summary>
/// Data structure for scene generation
/// </summary>
public struct SceneGeneration
{
    public Scene[] Scenes { get; set; }
}

/// <summary>
/// Step 4: Generate bosses and their trash mobs
/// </summary>
public class Step4_BossesRunnable : OrchestrationRunnable<GenerationStepResult, GenerationStepResult>
{
    private TornadoAgent Generator;
    private Adventure Adventure;

    public Step4_BossesRunnable(TornadoApi client, Orchestration orchestrator, Adventure adventure)
        : base(orchestrator)
    {
        Adventure = adventure;

        string instructions = """
            You are a D&D encounter designer. Create challenging boss encounters.
            Each boss should have:
            - Unique abilities and tactics
            - Stats scaled to the adventure difficulty
            - Positioned in appropriate scenes
            - Thematic loot appropriate to the boss
            
            Make bosses memorable and challenging for the players.
            """;

        Generator = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.O,
            name: "Boss Designer",
            instructions: instructions,
            outputSchema: typeof(BossGeneration));
    }

    public override async ValueTask<GenerationStepResult> Invoke(RunnableProcess<GenerationStepResult, GenerationStepResult> process)
    {
        process.RegisterAgent(Generator);

        Console.WriteLine("\n🎲 Step 4/7: Generating bosses and major encounters...");

        var sceneList = string.Join(", ", Adventure.Scenes.Values.Select(s => $"{s.Name} (ID: {s.Id})"));

        var prompt = $"""
            Generate boss encounters for the adventure "{Adventure.Name}".
            Difficulty: {Adventure.Difficulty}
            Setting: {Adventure.Setting}
            
            Available scenes: {sceneList}
            
            Create bosses with:
            - Stats scaled to {Adventure.Difficulty} difficulty
            - Positions within their scenes 
            - Unique abilities
            - Associated trash mobs if appropriate
            - Thematic loot
            
            Create at least one major boss for every 5 main quests.
            """;

        var messages = new List<ChatMessage> { new ChatMessage(ChatMessageRoles.User, prompt) };
        var conv = await Generator.Run(appendMessages: messages);
        BossGeneration? result = await conv.Messages.Last().Content?.SmartParseJsonAsync<BossGeneration>(Generator);

        if (result == null || !result.HasValue)
        {
            Console.WriteLine("❌ Failed to generate bosses");
            return new GenerationStepResult { Success = false, Message = "Failed to generate bosses" };
        }

        Adventure.Bosses = result.Value.Bosses;

        Console.WriteLine($"✅ Generated {Adventure.Bosses.Count} boss encounters");

        return new GenerationStepResult { Success = true, Message = "Bosses generated" };
    }
}

/// <summary>
/// Data structure for boss generation
/// </summary>
public struct BossGeneration
{
    public List<Boss> Bosses { get; set; }
}

/// <summary>
/// Step 5: Generate side quests
/// </summary>
public class Step5_SideQuestsRunnable : OrchestrationRunnable<GenerationStepResult, GenerationStepResult>
{
    private TornadoAgent Generator;
    private Adventure Adventure;

    public Step5_SideQuestsRunnable(TornadoApi client, Orchestration orchestrator, Adventure adventure)
        : base(orchestrator)
    {
        Adventure = adventure;

        string instructions = """
            You are a D&D quest designer. Create optional side quests that add depth to the adventure.
            Side quests should:
            - Be independent from the main story but enhance it
            - Offer unique rewards
            - Provide character development opportunities
            - Be fun diversions from the main quest
            """;

        Generator = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.OMini,
            name: "Side Quest Designer",
            instructions: instructions,
            outputSchema: typeof(QuestLineGeneration));
    }

    public override async ValueTask<GenerationStepResult> Invoke(RunnableProcess<GenerationStepResult, GenerationStepResult> process)
    {
        process.RegisterAgent(Generator);

        Console.WriteLine("\n🎲 Step 5/7: Generating side quests...");

        var prompt = $"""
            Generate side quests for the adventure "{Adventure.Name}".
            Setting: {Adventure.Setting}
            
            Create 5-10 optional side quests that players can discover.
            Make them interesting and rewarding without being required for main story completion.
            """;

        var messages = new List<ChatMessage> { new ChatMessage(ChatMessageRoles.User, prompt) };
        var conv = await Generator.Run(appendMessages: messages);
        QuestLineGeneration? result = await conv.Messages.Last().Content?.SmartParseJsonAsync<QuestLineGeneration>(Generator);

        if (result == null)
        {
            Console.WriteLine("⚠️ No side quests generated, continuing...");
            return new GenerationStepResult { Success = true, Message = "No side quests" };
        }

        foreach (var quest in result.Value.Quests)
        {
            quest.Type = QuestType.Side;
        }

        Adventure.SideQuests = result.Value.Quests.ToList();

        Console.WriteLine($"✅ Generated {Adventure.SideQuests.Count} side quests");

        return new GenerationStepResult { Success = true, Message = "Side quests generated" };
    }
}

/// <summary>
/// Step 7: Generate rare events
/// </summary>
public class Step7_RareEventsRunnable : OrchestrationRunnable<GenerationStepResult, GenerationStepResult>
{
    private TornadoAgent Generator;
    private Adventure Adventure;

    public Step7_RareEventsRunnable(TornadoApi client, Orchestration orchestrator, Adventure adventure)
        : base(orchestrator)
    {
        Adventure = adventure;

        string instructions = """
            You are a D&D content designer. Create rare and special events for the adventure.
            These should:
            - Have low trigger chances
            - Offer unique rewards
            - Create memorable moments
            - Include special loot, hidden bosses, or secret areas
            """;

        Generator = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.OMini,
            name: "Rare Event Designer",
            instructions: instructions,
            outputSchema: typeof(RareEventGeneration));
    }

    public override async ValueTask<GenerationStepResult> Invoke(RunnableProcess<GenerationStepResult, GenerationStepResult> process)
    {
        process.RegisterAgent(Generator);

        Console.WriteLine("\n🎲 Step 7/7: Generating rare events and secrets...");

        var prompt = $"""
            Generate rare events for the adventure "{Adventure.Name}".
            Setting: {Adventure.Setting}
            
            Create 5-10 rare events including:
            - Hidden treasure caches
            - Secret boss encounters
            - Special NPCs
            - Mysterious locations
            - Unique puzzles
            
            Make them rare (low trigger chance) but very rewarding.
            """;

        var messages = new List<ChatMessage> { new ChatMessage(ChatMessageRoles.User, prompt) };
        var conv = await Generator.Run(appendMessages: messages);
        RareEventGeneration? result = await conv.Messages.Last().Content?.SmartParseJsonAsync<RareEventGeneration>(Generator);

        if (result == null || !result.HasValue)
        {
            Console.WriteLine("⚠️ No rare events generated, continuing...");
            return new GenerationStepResult { Success = true, Message = "No rare events" };
        }

        Adventure.RareEvents = result.Value.Events;

        Console.WriteLine($"✅ Generated {Adventure.RareEvents.Count} rare events");
        Console.WriteLine("\n🎉 Adventure generation complete!");

        return new GenerationStepResult { Success = true, Message = "Rare events generated" };
    }
}

/// <summary>
/// Data structure for rare event generation
/// </summary>
public struct RareEventGeneration
{
    public List<RareEvent> Events { get; set; }
}
