using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Common;
using LlmTornado.Code;

namespace LlmTornado.Agents.Dnd.Agents;

/// <summary>
/// Configuration for the D&D game orchestration
/// </summary>
public class DndGameConfiguration : OrchestrationRuntimeConfiguration
{
    public GameState GameState { get; set; }
    public TornadoApi Client { get; set; }
    
    private DungeonMasterRunnable dungeonMaster;
    private PlayerActionRunnable playerAction;
    private NPCActionRunnable npcAction;
    private GameUpdateRunnable gameUpdate;
    private ExitRunnable exit;

    public DndGameConfiguration(TornadoApi client, GameState gameState)
    {
        Client = client;
        GameState = gameState;
        RecordSteps = true;

        // Create the runnables
        dungeonMaster = new DungeonMasterRunnable(Client, this, GameState);
        playerAction = new PlayerActionRunnable(this, GameState);
        npcAction = new NPCActionRunnable(Client, this, GameState);
        gameUpdate = new GameUpdateRunnable(this, GameState);
        exit = new ExitRunnable(this) { AllowDeadEnd = true };

        // Setup the orchestration flow
        dungeonMaster.AddAdvancer((response) => !string.IsNullOrEmpty(response.Narrative), playerAction);
        playerAction.AddAdvancer((action) => !string.IsNullOrEmpty(action.ActionType), npcAction);
        npcAction.AddAdvancer((actions) => actions != null, gameUpdate);
        gameUpdate.AddAdvancer((updated) => updated, dungeonMaster); // Loop back for next turn
        
        // Exit condition (can be triggered manually)
        gameUpdate.AddAdvancer((updated) => !updated, exit);

        // Configure entry and exit points
        SetEntryRunnable(dungeonMaster);
        SetRunnableWithResult(exit);
    }
}

/// <summary>
/// Dungeon Master agent that narrates the game and responds to actions
/// </summary>
public class DungeonMasterRunnable : OrchestrationRunnable<ChatMessage, DMResponse>
{
    private TornadoAgent Agent;
    private GameState GameState;

    public DungeonMasterRunnable(TornadoApi client, Orchestration orchestrator, GameState gameState) 
        : base(orchestrator)
    {
        GameState = gameState;
        
        string instructions = """
            You are an experienced Dungeon Master running a Dungeons & Dragons adventure.
            
            Your role is to:
            - Describe the current scene vividly and engagingly
            - Respond to player actions with narrative flair
            - Control NPCs and enemies
            - Create challenging and interesting scenarios
            - Keep track of combat, skill checks, and game mechanics
            - Make the game fun and immersive
            
            Be creative, descriptive, and engaging. React dynamically to player choices.
            When describing actions, include the outcomes and any changes to the game state.
            """;

        Agent = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.O,
            name: "Dungeon Master",
            instructions: instructions,
            outputSchema: typeof(DMResponse));
    }

    public override async ValueTask<DMResponse> Invoke(RunnableProcess<ChatMessage, DMResponse> process)
    {
        process.RegisterAgent(agent: Agent);

        // Build context about current game state
        var currentLocation = GameState.Locations[GameState.CurrentLocationName];
        var contextMessage = $"""
            Current Location: {currentLocation.Name}
            Description: {currentLocation.Description}
            Players Present: {string.Join(", ", GameState.Players.Select(p => $"{p.Name} (HP: {p.Health}/{p.MaxHealth})"))}
            Available Exits: {string.Join(", ", currentLocation.Exits)}
            NPCs Here: {string.Join(", ", currentLocation.NPCs)}
            Turn: {GameState.TurnNumber}
            
            {process.Input.Content}
            """;

        var messages = new List<ChatMessage> 
        { 
            new ChatMessage(ChatMessageRoles.User, contextMessage) 
        };

        Conversation conv = await Agent.Run(appendMessages: messages);
        DMResponse? response = await conv.Messages.Last().Content?.SmartParseJsonAsync<DMResponse>(Agent);

        if (response == null || string.IsNullOrEmpty(response.Value.Narrative))
        {
            return new DMResponse 
            { 
                Narrative = "The DM pauses, considering the next move...",
                ActionResult = "Waiting for input"
            };
        }

        return response.Value;
    }
}

/// <summary>
/// Handles player action input (user or AI)
/// </summary>
public class PlayerActionRunnable : OrchestrationRunnable<DMResponse, PlayerAction>
{
    private GameState GameState;

    public PlayerActionRunnable(Orchestration orchestrator, GameState gameState) 
        : base(orchestrator)
    {
        GameState = gameState;
    }

    public override async ValueTask<PlayerAction> Invoke(RunnableProcess<DMResponse, PlayerAction> process)
    {
        // Display DM response
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine(process.Input.Narrative);
        Console.WriteLine(new string('=', 80) + "\n");

        if (!string.IsNullOrEmpty(process.Input.ActionResult))
        {
            Console.WriteLine($"Result: {process.Input.ActionResult}\n");
        }

        // Get human player's action
        var humanPlayer = GameState.Players.FirstOrDefault(p => !p.IsAI);
        if (humanPlayer != null)
        {
            Console.WriteLine($"\n{humanPlayer.Name}, what do you do?");
            Console.WriteLine("Commands: [explore/move] [location], [talk] [npc], [attack] [target], [use] [item], [inventory], [status], [quit]");
            Console.Write("> ");
            
            string? input = Console.ReadLine();
            
            if (string.IsNullOrEmpty(input))
            {
                return new PlayerAction("wait", "", "Waiting", null);
            }

            if (input.ToLower() == "quit")
            {
                return new PlayerAction("quit", "", "Exiting game", null);
            }

            if (input.ToLower() == "inventory")
            {
                Console.WriteLine($"\nInventory: {string.Join(", ", humanPlayer.Inventory)}");
                return new PlayerAction("inventory", "", "Checked inventory", null);
            }

            if (input.ToLower() == "status")
            {
                Console.WriteLine($"\n{humanPlayer.Name} - {humanPlayer.Class} {humanPlayer.Race}");
                Console.WriteLine($"Level: {humanPlayer.Level} | HP: {humanPlayer.Health}/{humanPlayer.MaxHealth}");
                Console.WriteLine($"Gold: {humanPlayer.Gold} | XP: {humanPlayer.Experience}");
                Console.WriteLine($"Stats: {string.Join(", ", humanPlayer.Stats.Select(s => $"{s.Key}:{s.Value}"))}");
                return new PlayerAction("status", "", "Checked status", null);
            }

            var parts = input.Split(' ', 2);
            string action = parts[0].ToLower();
            string target = parts.Length > 1 ? parts[1] : "";

            return new PlayerAction(action, target, input, null);
        }

        return new PlayerAction("wait", "", "No players available", null);
    }
}

/// <summary>
/// Handles AI-controlled player/NPC actions
/// </summary>
public class NPCActionRunnable : OrchestrationRunnable<PlayerAction, List<PlayerAction>>
{
    private TornadoAgent Agent;
    private GameState GameState;

    public NPCActionRunnable(TornadoApi client, Orchestration orchestrator, GameState gameState) 
        : base(orchestrator)
    {
        GameState = gameState;

        string instructions = """
            You are an AI player in a D&D adventure. Based on the current situation,
            decide what action your character would take. Be creative and stay in character.
            Consider your character's class, personality, and the current situation.
            """;

        Agent = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.OMini,
            name: "AI Player",
            instructions: instructions);
    }

    public override async ValueTask<List<PlayerAction>> Invoke(RunnableProcess<PlayerAction, List<PlayerAction>> process)
    {
        var actions = new List<PlayerAction> { process.Input };

        // Get AI player actions
        var aiPlayers = GameState.Players.Where(p => p.IsAI).ToList();
        
        if (aiPlayers.Count > 0)
        {
            process.RegisterAgent(agent: Agent);

            foreach (var aiPlayer in aiPlayers)
            {
                var currentLocation = GameState.Locations[GameState.CurrentLocationName];
                var contextMessage = $"""
                    You are {aiPlayer.Name}, a {aiPlayer.Race} {aiPlayer.Class}.
                    Current situation: {currentLocation.Description}
                    Your HP: {aiPlayer.Health}/{aiPlayer.MaxHealth}
                    Available actions: explore, talk, attack, use item, rest
                    What do you do? Respond with a brief action description.
                    """;

                var messages = new List<ChatMessage> 
                { 
                    new ChatMessage(ChatMessageRoles.User, contextMessage) 
                };

                Conversation conv = await Agent.Run(appendMessages: messages);
                string actionDescription = conv.Messages.Last().Content ?? "waits";

                actions.Add(new PlayerAction("ai_action", aiPlayer.Name, actionDescription, null));
                Console.WriteLine($"\n{aiPlayer.Name} (AI): {actionDescription}");
            }
        }

        await Task.Delay(500); // Small delay for readability
        return actions;
    }
}

/// <summary>
/// Updates game state based on actions taken
/// </summary>
public class GameUpdateRunnable : OrchestrationRunnable<List<PlayerAction>, bool>
{
    private GameState GameState;

    public GameUpdateRunnable(Orchestration orchestrator, GameState gameState) 
        : base(orchestrator)
    {
        GameState = gameState;
    }

    public override ValueTask<bool> Invoke(RunnableProcess<List<PlayerAction>, bool> process)
    {
        GameState.TurnNumber++;

        // Check for quit action
        if (process.Input.Any(a => a.ActionType == "quit"))
        {
            return ValueTask.FromResult(false); // Signal to exit
        }

        // Process movement
        var moveAction = process.Input.FirstOrDefault(a => a.ActionType == "move" || a.ActionType == "explore");
        if (!string.IsNullOrEmpty(moveAction.Target))
        {
            var currentLocation = GameState.Locations[GameState.CurrentLocationName];
            var targetLocation = currentLocation.Exits.FirstOrDefault(e => 
                e.ToLower().Contains(moveAction.Target.ToLower()));
            
            if (targetLocation != null)
            {
                GameState.CurrentLocationName = targetLocation;
                GameState.Locations[targetLocation].IsVisited = true;
            }
        }

        // Add actions to history
        foreach (var action in process.Input)
        {
            GameState.GameHistory.Add($"Turn {GameState.TurnNumber}: {action.Description}");
        }

        return ValueTask.FromResult(true); // Continue game loop
    }
}

/// <summary>
/// Exit runnable for game completion
/// </summary>
public class ExitRunnable : OrchestrationRunnable<bool, ChatMessage>
{
    public ExitRunnable(Orchestration orchestrator, string runnableName = "") 
        : base(orchestrator, runnableName)
    {
    }

    public override async ValueTask<ChatMessage> Invoke(RunnableProcess<bool, ChatMessage> process)
    {
        this.Orchestrator?.HasCompletedSuccessfully();
        
        string message = process.Input 
            ? "Game session continues..." 
            : "Thank you for playing! Your progress has been saved.";
            
        return new ChatMessage(ChatMessageRoles.Assistant, message);
    }
}
