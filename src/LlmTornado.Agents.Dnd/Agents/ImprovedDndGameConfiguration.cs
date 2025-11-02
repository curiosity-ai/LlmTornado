using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Common;
using LlmTornado.Code;

namespace LlmTornado.Agents.Dnd.Agents;

/// <summary>
/// Improved configuration for the D&D game with proper phase management
/// </summary>
public class ImprovedDndGameConfiguration : OrchestrationRuntimeConfiguration
{
    public GameState GameState { get; set; }
    public TornadoApi Client { get; set; }
    public CombatManager CombatManager { get; set; }
    public Adventure? CurrentAdventure { get; set; }
    
    private PhaseManagerRunnable phaseManager;
    private AdventuringPhaseRunnable adventuringPhase;
    private CombatPhaseRunnable combatPhase;
    private ImprovedExitRunnable exit;

    public ImprovedDndGameConfiguration(TornadoApi client, GameState gameState, Adventure? adventure = null)
    {
        Client = client;
        GameState = gameState;
        CurrentAdventure = adventure;
        CombatManager = new CombatManager(gameState, client);
        RecordSteps = true;

        // Create the runnables
        phaseManager = new PhaseManagerRunnable(this, GameState, CombatManager);
        adventuringPhase = new AdventuringPhaseRunnable(Client, this, GameState, CombatManager, adventure);
        combatPhase = new CombatPhaseRunnable(this, GameState, CombatManager);
        exit = new ImprovedExitRunnable(this) { AllowDeadEnd = true };

        // Setup the orchestration flow based on phase
        phaseManager.AddAdvancer(
            result => result.ShouldContinue && result.CurrentPhase == GamePhase.Adventuring,
            adventuringPhase);
        
        phaseManager.AddAdvancer(
            result => result.ShouldContinue && result.CurrentPhase == GamePhase.Combat,
            combatPhase);

        adventuringPhase.AddAdvancer(
            result => result.ShouldContinue,
            (result) => new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = true },
            phaseManager);

        adventuringPhase.AddAdvancer(result => !result.ShouldContinue, exit);

        combatPhase.AddAdvancer(
            result => result.ShouldContinue,
            (result) => new PhaseResult { CurrentPhase = GameState.CurrentPhase, ShouldContinue = true },
            phaseManager);

        combatPhase.AddAdvancer(result => !result.ShouldContinue, exit);

        // Configure entry and exit points
        SetEntryRunnable(phaseManager);
        SetRunnableWithResult(exit);
    }
}

/// <summary>
/// Manages phase transitions and game state
/// </summary>
public class PhaseManagerRunnable : OrchestrationRunnable<ChatMessage, PhaseResult>
{
    private GameState GameState;
    private CombatManager CombatManager;

    public PhaseManagerRunnable(Orchestration orchestrator, GameState gameState, CombatManager combatManager) 
        : base(orchestrator)
    {
        GameState = gameState;
        CombatManager = combatManager;
    }

    public override ValueTask<PhaseResult> Invoke(RunnableProcess<ChatMessage, PhaseResult> process)
    {
        // Check if combat is active but ended
        if (GameState.CurrentPhase == GamePhase.Combat && 
            GameState.CombatState != null && 
            GameState.CombatState.IsCombatOver())
        {
            string endMessage = CombatManager.EndCombat();
            Console.WriteLine("\n" + new string('═', 80));
            Console.WriteLine(endMessage);
            Console.WriteLine(new string('═', 80) + "\n");
        }

        return ValueTask.FromResult(new PhaseResult
        {
            CurrentPhase = GameState.CurrentPhase,
            ShouldContinue = true
        });
    }
}

/// <summary>
/// Result of phase management
/// </summary>
public struct PhaseResult
{
    public GamePhase CurrentPhase { get; set; }
    public bool ShouldContinue { get; set; }
}

/// <summary>
/// Handles the adventuring phase with DM narration
/// </summary>
public class AdventuringPhaseRunnable : OrchestrationRunnable<PhaseResult, PhaseResult>
{
    private TornadoAgent DungeonMaster;
    private GameState GameState;
    private CombatManager CombatManager;
    private Adventure? Adventure;

    public AdventuringPhaseRunnable(TornadoApi client, Orchestration orchestrator, GameState gameState, CombatManager combatManager, Adventure? adventure = null) 
        : base(orchestrator)
    {
        GameState = gameState;
        CombatManager = combatManager;
        Adventure = adventure;

        string instructions = Adventure != null
            ? $"""
            You are an experienced Dungeon Master running the adventure: "{Adventure.Name}"
            
            Adventure Description: {Adventure.Description}
            Setting: {Adventure.Setting}
            Difficulty: {Adventure.Difficulty}
            
            Current Quest Progress: {Adventure.CompletedQuestIds.Count}/{Adventure.MainQuestLine.Count} main quests completed
            
            Your role is to:
            - Follow the adventure structure loosely - use it as a guide
            - Describe scenes vividly and engagingly based on the generated world
            - Respond to player actions with narrative flair
            - Control NPCs and the environment according to the adventure
            - Progress the main quest line naturally when appropriate
            - Create interesting scenarios aligned with the adventure theme
            - Decide when combat should be initiated based on encounters in the adventure
            - Make the game fun and immersive
            
            Reference the generated quests, scenes, and NPCs but don't feel constrained by them.
            Use your creativity to enhance the experience.
            
            When combat should begin, set CombatInitiated to true and provide enemy names from the adventure.
            """
            : """
            You are an experienced Dungeon Master running a Dungeons & Dragons adventure.
            
            Your role is to:
            - Describe scenes vividly and engagingly
            - Respond to player actions with narrative flair
            - Control NPCs and the environment
            - Create interesting scenarios and encounters
            - Decide when combat should be initiated based on player actions or random encounters
            - Make the game fun and immersive
            
            When combat should begin, set CombatInitiated to true and provide a list of enemy names in the response.
            Be creative and dynamic. React to player choices meaningfully.
            """;

        DungeonMaster = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.O,
            name: "Dungeon Master",
            instructions: instructions,
            outputSchema: typeof(DMResponse));
    }

    public override async ValueTask<PhaseResult> Invoke(RunnableProcess<PhaseResult, PhaseResult> process)
    {
        process.RegisterAgent(agent: DungeonMaster);

        // Get current location
        var currentLocation = GameState.Locations[GameState.CurrentLocationName];
        
        // Display location info
        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine($"📍 {currentLocation.Name}");
        Console.WriteLine(new string('═', 80));
        Console.WriteLine(currentLocation.Description);
        if (currentLocation.Exits.Any())
        {
            Console.WriteLine($"\n🚪 Exits: {string.Join(", ", currentLocation.Exits)}");
        }
        if (currentLocation.NPCs.Any())
        {
            Console.WriteLine($"👤 NPCs: {string.Join(", ", currentLocation.NPCs)}");
        }
        Console.WriteLine(new string('═', 80) + "\n");

        // Get player action
        var humanPlayer = GameState.Players.FirstOrDefault(p => !p.IsAI);
        if (humanPlayer == null)
        {
            return new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = false };
        }

        Console.WriteLine($"{humanPlayer.Name}, what do you do?");
        Console.WriteLine(ActionParser.GetAvailableCommands(GamePhase.Adventuring));
        Console.Write("> ");
        
        string? input = Console.ReadLine();
        var action = ActionParser.ParseAction(input ?? "", humanPlayer.Name, GamePhase.Adventuring);

        // Handle special actions locally
        if (action.Type == ActionType.Quit)
        {
            return new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = false };
        }

        if (action.Type == ActionType.ViewInventory)
        {
            Console.WriteLine($"\n💼 Inventory: {string.Join(", ", humanPlayer.Inventory)}");
            return new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = true };
        }

        if (action.Type == ActionType.ViewStatus)
        {
            Console.WriteLine($"\n📊 {humanPlayer.Name} - {humanPlayer.Race} {humanPlayer.Class}");
            Console.WriteLine($"Level: {humanPlayer.Level} | HP: {humanPlayer.Health}/{humanPlayer.MaxHealth}");
            Console.WriteLine($"Gold: {humanPlayer.Gold} | XP: {humanPlayer.Experience}");
            Console.WriteLine($"Stats: {string.Join(", ", humanPlayer.Stats.Select(s => $"{s.Key}:{s.Value}"))}");
            return new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = true };
        }

        // Handle movement
        if (action.Type == ActionType.Move && !string.IsNullOrEmpty(action.Target))
        {
            var targetLocation = currentLocation.Exits.FirstOrDefault(e => 
                e.ToLower().Contains(action.Target.ToLower()));
            
            if (targetLocation != null)
            {
                GameState.CurrentLocationName = targetLocation;
                GameState.GameHistory.Add($"Turn {++GameState.TurnNumber}: Moved to {targetLocation}");
                Console.WriteLine($"\n✅ Moving to {targetLocation}...\n");
                return new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = true };
            }
            else
            {
                Console.WriteLine($"\n❌ Can't go to '{action.Target}' from here.\n");
                return new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = true };
            }
        }

        // Ask DM to respond to action
        var adventureContext = Adventure != null
            ? $"""
            
            === ADVENTURE CONTEXT ===
            Current Quest: {GetCurrentQuestInfo()}
            Available Scenes: {GetNearbyScenes()}
            Potential Encounters: {GetPotentialEncounters()}
            """
            : "";

        var contextMessage = $"""
            Current Location: {currentLocation.Name}
            Players Present: {string.Join(", ", GameState.Players.Select(p => $"{p.Name} (HP: {p.Health}/{p.MaxHealth})"))}
            Turn: {GameState.TurnNumber}
            {adventureContext}
            
            Player Action: {action}
            """;

        var messages = new List<ChatMessage> { new ChatMessage(ChatMessageRoles.User, contextMessage) };
        Conversation conv = await DungeonMaster.Run(appendMessages: messages);
        DMResponse? response = await conv.Messages.Last().Content?.SmartParseJsonAsync<DMResponse>(DungeonMaster);

        if (response == null)
        {
            Console.WriteLine("\n❌ DM response error. Continuing...\n");
            return new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = true };
        }

        // Display DM response
        Console.WriteLine("\n" + new string('─', 80));
        Console.WriteLine(response.Value.Narrative);
        Console.WriteLine(new string('─', 80) + "\n");

        // Check if combat initiated
        if (response.Value.CombatInitiated)
        {
            var enemies = response.Value.NewQuestItems ?? Array.Empty<string>();
            if (enemies.Length == 0)
            {
                enemies = new[] { "Goblin", "Wolf" }; // Default enemies
            }
            
            await CombatManager.InitiateCombatAsync(enemies.ToList(), response.Value.Narrative);
            Console.WriteLine("\n⚔️ Combat has begun!\n");
            return new PhaseResult { CurrentPhase = GamePhase.Combat, ShouldContinue = true };
        }

        GameState.TurnNumber++;
        return new PhaseResult { CurrentPhase = GamePhase.Adventuring, ShouldContinue = true };
    }

    private string GetCurrentQuestInfo()
    {
        if (Adventure == null || Adventure.CurrentQuestIndex >= Adventure.MainQuestLine.Count)
        {
            return "No active quest";
        }

        var quest = Adventure.MainQuestLine[Adventure.CurrentQuestIndex];
        return $"{quest.Name} - {quest.Description}";
    }

    private string GetNearbyScenes()
    {
        if (Adventure == null || !Adventure.Scenes.Any())
        {
            return "No scenes available";
        }

        return string.Join(", ", Adventure.Scenes.Values.Take(5).Select(s => s.Name));
    }

    private string GetPotentialEncounters()
    {
        if (Adventure == null)
        {
            return "Random encounters possible";
        }

        var encounters = new List<string>();
        
        // Check for trash mobs in current area
        var nearbyMobs = Adventure.TrashMobs.Take(3).Select(m => m.Name);
        encounters.AddRange(nearbyMobs);

        // Check for bosses
        var nearbyBosses = Adventure.Bosses.Take(2).Select(b => b.Name);
        encounters.AddRange(nearbyBosses);

        return encounters.Any() ? string.Join(", ", encounters) : "None specifically planned";
    }
}

/// <summary>
/// Handles the combat phase
/// </summary>
public class CombatPhaseRunnable : OrchestrationRunnable<PhaseResult, PhaseResult>
{
    private GameState GameState;
    private CombatManager CombatManager;

    public CombatPhaseRunnable(Orchestration orchestrator, GameState gameState, CombatManager combatManager) 
        : base(orchestrator)
    {
        GameState = gameState;
        CombatManager = combatManager;
    }

    public override ValueTask<PhaseResult> Invoke(RunnableProcess<PhaseResult, PhaseResult> process)
    {
        if (GameState.CombatState == null || !GameState.CombatState.IsActive)
        {
            return ValueTask.FromResult(new PhaseResult 
            { 
                CurrentPhase = GamePhase.Adventuring, 
                ShouldContinue = true 
            });
        }

        // Display combat grid
        Console.WriteLine(CombatManager.GetCombatDisplay());

        var currentEntity = GameState.CombatState.GetCurrentEntity();
        if (currentEntity == null)
        {
            return ValueTask.FromResult(new PhaseResult 
            { 
                CurrentPhase = GamePhase.Combat, 
                ShouldContinue = true 
            });
        }

        GameAction action;

        if (currentEntity.IsPlayer)
        {
            // Human player action
            Console.WriteLine($"\n{currentEntity.Name}'s turn!");
            Console.WriteLine(ActionParser.GetAvailableCommands(GamePhase.Combat));
            Console.Write("> ");
            
            string? input = Console.ReadLine();
            action = ActionParser.ParseAction(input ?? "", currentEntity.Name, GamePhase.Combat);

            if (action.Type == ActionType.Quit)
            {
                return ValueTask.FromResult(new PhaseResult 
                { 
                    CurrentPhase = GamePhase.Combat, 
                    ShouldContinue = false 
                });
            }

            if (action.Type == ActionType.ViewInventory)
            {
                var player = GameState.Players.FirstOrDefault(p => p.Name == currentEntity.Name);
                if (player != null)
                {
                    Console.WriteLine($"\n💼 Inventory: {string.Join(", ", player.Inventory)}");
                }
                return ValueTask.FromResult(new PhaseResult 
                { 
                    CurrentPhase = GamePhase.Combat, 
                    ShouldContinue = true 
                });
            }

            if (action.Type == ActionType.ViewStatus)
            {
                Console.WriteLine($"\n📊 {currentEntity.Name}: {currentEntity.Health}/{currentEntity.MaxHealth} HP");
                Console.WriteLine($"Position: {currentEntity.Position} | Attack: {currentEntity.AttackPower} | Defense: {currentEntity.Defense}");
                return ValueTask.FromResult(new PhaseResult 
                { 
                    CurrentPhase = GamePhase.Combat, 
                    ShouldContinue = true 
                });
            }
        }
        else
        {
            // AI enemy action - simple AI
            var players = GameState.CombatState.Entities.Where(e => e.IsPlayer && !e.IsDefeated).ToList();
            if (players.Any())
            {
                var nearestPlayer = players.OrderBy(p => currentEntity.Position.DistanceTo(p.Position)).First();
                
                if (currentEntity.Position.DistanceTo(nearestPlayer.Position) <= 1)
                {
                    // Attack if adjacent
                    action = new GameAction
                    {
                        Type = ActionType.Attack,
                        Target = nearestPlayer.Name,
                        PlayerName = currentEntity.Name,
                        Description = $"Attack {nearestPlayer.Name}"
                    };
                }
                else
                {
                    // Move closer
                    int newX = currentEntity.Position.X;
                    int newY = currentEntity.Position.Y;
                    
                    if (currentEntity.Position.X < nearestPlayer.Position.X) newX++;
                    else if (currentEntity.Position.X > nearestPlayer.Position.X) newX--;
                    
                    if (currentEntity.Position.Y < nearestPlayer.Position.Y) newY++;
                    else if (currentEntity.Position.Y > nearestPlayer.Position.Y) newY--;
                    
                    action = new GameAction
                    {
                        Type = ActionType.CombatMove,
                        PlayerName = currentEntity.Name,
                        Description = "Move closer",
                        Parameters = new Dictionary<string, string>
                        {
                            { "x", newX.ToString() },
                            { "y", newY.ToString() }
                        }
                    };
                }
            }
            else
            {
                action = new GameAction
                {
                    Type = ActionType.Defend,
                    PlayerName = currentEntity.Name,
                    Description = "Defend"
                };
            }
        }

        // Process the action
        string result = CombatManager.ProcessCombatAction(action);
        Console.WriteLine($"\n⚔️ {result}\n");

        GameState.GameHistory.Add($"Turn {++GameState.TurnNumber}: {action}");

        return ValueTask.FromResult(new PhaseResult 
        { 
            CurrentPhase = GamePhase.Combat, 
            ShouldContinue = true 
        });
    }
}

/// <summary>
/// Exit runnable for improved game completion
/// </summary>
public class ImprovedExitRunnable : OrchestrationRunnable<PhaseResult, ChatMessage>
{
    public ImprovedExitRunnable(Orchestration orchestrator, string runnableName = "") 
        : base(orchestrator, runnableName)
    {
    }

    public override ValueTask<ChatMessage> Invoke(RunnableProcess<PhaseResult, ChatMessage> process)
    {
        this.Orchestrator?.HasCompletedSuccessfully();
        
        string message = process.Input.ShouldContinue
            ? "Game session continues..." 
            : "Thank you for playing! Your progress has been saved.";
            
        return ValueTask.FromResult(new ChatMessage(ChatMessageRoles.Assistant, message));
    }
}
