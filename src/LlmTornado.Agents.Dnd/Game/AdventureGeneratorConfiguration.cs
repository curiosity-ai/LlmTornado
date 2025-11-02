using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Persistence;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Common;
using LlmTornado.Code;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Orchestrates the progressive generation of adventures
/// </summary>
public class AdventureGeneratorConfiguration : OrchestrationRuntimeConfiguration
{
    public TornadoApi Client { get; set; }
    public Adventure Adventure { get; set; }
    public AdventurePersistence Persistence { get; set; }

    private Step1_DescriptionRunnable step1;
    private Step2_MainQuestLineRunnable step2;
    private Step3_ScenesRunnable step3;
    private Step4_BossesRunnable step4;
    private Step5_SideQuestsRunnable step5;
    private Step6_TrashMobsRunnable step6;
    private Step7_RareEventsRunnable step7;
    private GeneratorExitRunnable exit;

    public AdventureGeneratorConfiguration(TornadoApi client, AdventurePersistence persistence, string? adventureSeed = null)
    {
        Client = client;
        Persistence = persistence;
        Adventure = new Adventure();
        RecordSteps = true;

        // Create runnables for each generation step
        step1 = new Step1_DescriptionRunnable(Client, this, Adventure, adventureSeed);
        step2 = new Step2_MainQuestLineRunnable(Client, this, Adventure);
        step3 = new Step3_ScenesRunnable(Client, this, Adventure);
        step4 = new Step4_BossesRunnable(Client, this, Adventure);
        step5 = new Step5_SideQuestsRunnable(Client, this, Adventure);
        step6 = new Step6_TrashMobsRunnable(Client, this, Adventure);
        step7 = new Step7_RareEventsRunnable(Client, this, Adventure);
        exit = new GeneratorExitRunnable(this, Adventure, Persistence);

        // Setup the progressive orchestration flow
        step1.AddAdvancer(result => result.Success, step2);
        step2.AddAdvancer(result => result.Success, step3);
        step3.AddAdvancer(result => result.Success, step4);
        step4.AddAdvancer(result => result.Success, step5);
        step5.AddAdvancer(result => result.Success, step6);
        step6.AddAdvancer(result => result.Success, step7);
        step7.AddAdvancer(result => result.Success, exit);

        // Error handling
        step1.AddAdvancer(result => !result.Success, exit);
        step2.AddAdvancer(result => !result.Success, exit);
        step3.AddAdvancer(result => !result.Success, exit);
        step4.AddAdvancer(result => !result.Success, exit);
        step5.AddAdvancer(result => !result.Success, exit);
        step6.AddAdvancer(result => !result.Success, exit);
        step7.AddAdvancer(result => !result.Success, exit);

        SetEntryRunnable(step1);
        SetRunnableWithResult(exit);
    }
}

/// <summary>
/// Result from generation steps
/// </summary>
public struct GenerationStepResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
}

/// <summary>
/// Step 1: Generate adventure description and difficulty
/// </summary>
public class Step1_DescriptionRunnable : OrchestrationRunnable<ChatMessage, GenerationStepResult>
{
    private TornadoAgent Generator;
    private Adventure Adventure;
    private string? Seed;

    public Step1_DescriptionRunnable(TornadoApi client, Orchestration orchestrator, Adventure adventure, string? seed)
        : base(orchestrator)
    {
        Adventure = adventure;
        Seed = seed;

        string instructions = """
            You are a creative D&D adventure designer. Generate an engaging adventure concept.
            Create a compelling name, description, appropriate difficulty level, and setting.
            Be creative and original. Consider classic fantasy themes but add unique twists.
            """;

        Generator = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.O,
            name: "Adventure Concept Designer",
            instructions: instructions,
            outputSchema: typeof(AdventureDescription));
    }

    public override async ValueTask<GenerationStepResult> Invoke(RunnableProcess<ChatMessage, GenerationStepResult> process)
    {
        process.RegisterAgent(Generator);

        Console.WriteLine("\n🎲 Step 1/7: Generating adventure concept...");

        var prompt = string.IsNullOrEmpty(Seed)
            ? "Generate a complete D&D adventure concept with name, description, difficulty level, and setting."
            : $"Generate a D&D adventure concept based on this theme: {Seed}";

        var messages = new List<ChatMessage> { new ChatMessage(ChatMessageRoles.User, prompt) };
        var conv = await Generator.Run(appendMessages: messages);
        AdventureDescription? result = await conv.Messages.Last().Content?.SmartParseJsonAsync<AdventureDescription>(Generator);

        if (result == null || !result.HasValue)
        {
            Console.WriteLine("❌ Failed to generate adventure description");
            return new GenerationStepResult { Success = false, Message = "Failed to generate description" };
        }

        var desc = result.Value;
        Adventure.Name = desc.Name;
        Adventure.Description = desc.Description;
        Adventure.Difficulty = desc.Difficulty;
        Adventure.Setting = desc.Setting;

        Console.WriteLine($"✅ Adventure: {Adventure.Name}");
        Console.WriteLine($"   Difficulty: {Adventure.Difficulty}");
        Console.WriteLine($"   {Adventure.Description.Substring(0, Math.Min(100, Adventure.Description.Length))}...");

        return new GenerationStepResult { Success = true, Message = "Description generated" };
    }
}

/// <summary>
/// Data structure for adventure description
/// </summary>
public struct AdventureDescription
{
    public string Name { get; set; }
    public string Description { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public string Setting { get; set; }
}

/// <summary>
/// Step 2: Generate main quest line (minimum 20 quests)
/// </summary>
public class Step2_MainQuestLineRunnable : OrchestrationRunnable<GenerationStepResult, GenerationStepResult>
{
    private TornadoAgent Generator;
    private Adventure Adventure;

    public Step2_MainQuestLineRunnable(TornadoApi client, Orchestration orchestrator, Adventure adventure)
        : base(orchestrator)
    {
        Adventure = adventure;

        string instructions = """
            You are a D&D quest designer. Create a compelling main quest line with minimum 20 interconnected quests.
            Each quest should have clear requirements, rewards, start events, and completion criteria.
            Make sure quests build on each other narratively and mechanically.
            Include varied quest types: combat, investigation, social, exploration, etc.
            """;

        Generator = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.O,
            name: "Quest Line Designer",
            instructions: instructions,
            outputSchema: typeof(QuestLineGeneration));
    }

    public override async ValueTask<GenerationStepResult> Invoke(RunnableProcess<GenerationStepResult, GenerationStepResult> process)
    {
        process.RegisterAgent(Generator);

        Console.WriteLine("\n🎲 Step 2/7: Generating main quest line (minimum 20 quests)...");

        var prompt = $"""
            Generate a main quest line for the adventure "{Adventure.Name}".
            Setting: {Adventure.Setting}
            Difficulty: {Adventure.Difficulty}
            Description: {Adventure.Description}
            
            Create at least 20 quests that form a cohesive story arc.
            Each quest should have dependencies (requirements), clear objectives, and appropriate rewards.
            """;

        var messages = new List<ChatMessage> { new ChatMessage(ChatMessageRoles.User, prompt) };
        var conv = await Generator.Run(appendMessages: messages);
        QuestLineGeneration? result = await conv.Messages.Last().Content?.SmartParseJsonAsync<QuestLineGeneration>(Generator);

        if (result == null || !result.HasValue || result.Value.Quests.Length < 20)
        {
            int questCount = result.HasValue && result.Value.Quests != null ? result.Value.Quests.Length : 0;
            Console.WriteLine($"❌ Failed to generate quest line (got {questCount} quests, need 20)");
            return new GenerationStepResult { Success = false, Message = "Failed to generate quest line" };
        }

        Adventure.MainQuestLine = result.Value.Quests.ToList();

        Console.WriteLine($"✅ Generated {Adventure.MainQuestLine.Count} main quests");

        return new GenerationStepResult { Success = true, Message = "Quest line generated" };
    }
}

/// <summary>
/// Data structure for quest line generation
/// </summary>
public struct QuestLineGeneration
{
    public Quest[] Quests { get; set; }
}

/// <summary>
/// Exit runnable that saves the adventure
/// </summary>
public class GeneratorExitRunnable : OrchestrationRunnable<GenerationStepResult, ChatMessage>
{
    private Adventure Adventure;
    private AdventurePersistence Persistence;

    public GeneratorExitRunnable(Orchestration orchestrator, Adventure adventure, AdventurePersistence persistence)
        : base(orchestrator)
    {
        Adventure = adventure;
        Persistence = persistence;
    }

    public override ValueTask<ChatMessage> Invoke(RunnableProcess<GenerationStepResult, ChatMessage> process)
    {
        if (process.Input.Success)
        {
            Console.WriteLine("\n💾 Saving adventure...");
            Persistence.SaveAdventure(Adventure);
            Console.WriteLine($"✅ Adventure '{Adventure.Name}' saved successfully!");
            Console.WriteLine($"   ID: {Adventure.Id}");
        }
        else
        {
            Console.WriteLine($"\n❌ Adventure generation incomplete: {process.Input.Message}");
        }

        this.Orchestrator?.HasCompletedSuccessfully();
        return ValueTask.FromResult(new ChatMessage(ChatMessageRoles.Assistant, "Adventure generation complete"));
    }
}
