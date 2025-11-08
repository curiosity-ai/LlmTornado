using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Agents.Dnd.Agents.Runnables;

/// <summary>
/// Handles the adventuring phase with DM narration and memory system
/// </summary>
public class AdventuringPhaseRunnable : OrchestrationRunnable<PhaseResult, PhaseResult>
{
    private readonly TornadoAgent _dungeonMaster;
    private readonly GameState _gameState;
    private readonly CombatManager _combatManager;
    private readonly Adventure? _adventure;
    private readonly ImprovedDndGameConfiguration _configuration;

    public AdventuringPhaseRunnable(
        TornadoApi client, 
        Orchestration orchestrator, 
        GameState gameState, 
        CombatManager combatManager, 
        Adventure? adventure = null) 
        : base(orchestrator)
    {
        _gameState = gameState;
        _combatManager = combatManager;
        _adventure = adventure;
        _configuration = (ImprovedDndGameConfiguration)orchestrator;

        string instructions = BuildDMInstructions(adventure);

        _dungeonMaster = new TornadoAgent(
            client: client,
            model: ChatModel.OpenAi.Gpt4.O,
            name: "Dungeon Master",
            instructions: instructions,
            outputSchema: typeof(DMResponse));
    }

    public override async ValueTask<PhaseResult> Invoke(RunnableProcess<PhaseResult, PhaseResult> process)
    {
        process.RegisterAgent(agent: _dungeonMaster);

        // Display current location
        var currentLocation = _gameState.Locations[_gameState.CurrentLocationName];
        DisplayLocation(currentLocation);

        // Get human player
        var humanPlayer = _gameState.Players.FirstOrDefault(p => !p.IsAI);
        if (humanPlayer == null)
        {
            return CreateResult(GamePhase.Adventuring, false);
        }

        // Get player action
        GameAction action = GetPlayerAction(humanPlayer);

        // Handle special actions
        if (HandleSpecialAction(action, humanPlayer))
        {
            return CreateResult(GamePhase.Adventuring, true);
        }

        // Handle movement
        if (await HandleMovementAsync(action, humanPlayer, currentLocation))
        {
            return CreateResult(GamePhase.Adventuring, true);
        }

        // Get DM response
        DMResponse? response = await GetDMResponseAsync(action, humanPlayer, currentLocation);
        if (response == null)
        {
            Console.WriteLine("\n❌ DM response error. Continuing...\n");
            return CreateResult(GamePhase.Adventuring, true);
        }

        // Display DM narrative
        DisplayNarrative(response.Value.Narrative);

        // Store interaction memory
        await StoreInteractionMemoryAsync(action, humanPlayer, response.Value, currentLocation);

        // Check for combat
        if (response.Value.CombatInitiated)
        {
            return await InitiateCombatAsync(response.Value, humanPlayer, currentLocation);
        }

        _gameState.TurnNumber++;
        return CreateResult(GamePhase.Adventuring, true);
    }
    
    /// <summary>
    /// Builds DM instructions based on whether an adventure is being used
    /// </summary>
    private string BuildDMInstructions(Adventure? adventure)
    {
        if (adventure != null)
        {
            return $"""
            You are an experienced Dungeon Master running the adventure: "{adventure.Name}"
            
            Adventure Description: {adventure.Description}
            Setting: {adventure.Setting}
            Difficulty: {adventure.Difficulty}
            
            Current Quest Progress: {adventure.CompletedQuestIds.Count}/{adventure.MainQuestLine.Count} main quests completed
            
            Your role is to:
            - Follow the adventure structure loosely - use it as a guide
            - Describe scenes vividly and engagingly based on the generated world
            - Respond to player actions with narrative flair
            - Control NPCs and the environment according to the adventure
            - Progress the main quest line naturally when appropriate
            - Create interesting scenarios aligned with the adventure theme
            - Decide when combat should be initiated based on encounters in the adventure
            - Make the game fun and immersive
            - STAY NEUTRAL and UNBIASED - do not favor any player or NPC
            
            Reference the generated quests, scenes, and NPCs but don't feel constrained by them.
            Use your creativity to enhance the experience.
            
            When combat should begin, set CombatInitiated to true and provide enemy names from the adventure.
            """;
        }
        
        return """
            You are an experienced Dungeon Master running a Dungeons & Dragons adventure.
            
            Your role is to:
            - Describe scenes vividly and engagingly
            - Respond to player actions with narrative flair
            - Control NPCs and the environment
            - Create interesting scenarios and encounters
            - Decide when combat should be initiated based on player actions or random encounters
            - Make the game fun and immersive
            - STAY NEUTRAL and UNBIASED - do not favor any player or NPC
            
            When combat should begin, set CombatInitiated to true and provide a list of enemy names in the response.
            Be creative and dynamic. React to player choices meaningfully.
            """;
    }
    
    /// <summary>
    /// Displays current location information
    /// </summary>
    private void DisplayLocation(Location location)
    {
        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine($"📍 {location.Name}");
        Console.WriteLine(new string('═', 80));
        Console.WriteLine(location.Description);
        
        if (location.Exits.Any())
        {
            Console.WriteLine($"\n🚪 Exits: {string.Join(", ", location.Exits)}");
        }
        
        if (location.NPCs.Any())
        {
            Console.WriteLine($"👤 NPCs: {string.Join(", ", location.NPCs)}");
        }
        
        Console.WriteLine(new string('═', 80) + "\n");
    }
    
    /// <summary>
    /// Gets action from human player
    /// </summary>
    private GameAction GetPlayerAction(PlayerCharacter player)
    {
        Console.WriteLine($"{player.Name}, what do you do?");
        Console.WriteLine(ActionParser.GetAvailableCommands(GamePhase.Adventuring));
        Console.Write("> ");
        
        string? input = Console.ReadLine();
        return ActionParser.ParseAction(input ?? "", player.Name, GamePhase.Adventuring);
    }
    
    /// <summary>
    /// Handles special actions like quit, inventory, status
    /// </summary>
    private bool HandleSpecialAction(GameAction action, PlayerCharacter player)
    {
        if (action.Type == ActionType.Quit)
        {
            return false; // Signal to exit
        }

        if (action.Type == ActionType.ViewInventory)
        {
            ShowInventory(player);
            return true;
        }

        if (action.Type == ActionType.ViewStatus)
        {
            ShowStatus(player);
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// Shows player inventory
    /// </summary>
    private void ShowInventory(PlayerCharacter player)
    {
        Console.WriteLine($"\n💼 Inventory: {string.Join(", ", player.Inventory)}");
    }
    
    /// <summary>
    /// Shows player status
    /// </summary>
    private void ShowStatus(PlayerCharacter player)
    {
        Console.WriteLine($"\n📊 {player.Name} - {player.Race} {player.Class}");
        Console.WriteLine($"Level: {player.Level} | HP: {player.Health}/{player.MaxHealth}");
        Console.WriteLine($"Gold: {player.Gold} | XP: {player.Experience}");
        Console.WriteLine($"Stats: {string.Join(", ", player.Stats.Select(s => $"{s.Key}:{s.Value}"))}");
    }
    
    /// <summary>
    /// Handles player movement between locations
    /// </summary>
    private async Task<bool> HandleMovementAsync(GameAction action, PlayerCharacter player, Location currentLocation)
    {
        if (action.Type != ActionType.Move || string.IsNullOrEmpty(action.Target))
        {
            return false;
        }

        var targetLocation = currentLocation.Exits.FirstOrDefault(e => 
            e.ToLower().Contains(action.Target.ToLower()));
        
        if (targetLocation != null)
        {
            _gameState.CurrentLocationName = targetLocation;
            _gameState.GameHistory.Add($"Turn {++_gameState.TurnNumber}: Moved to {targetLocation}");
            
            // Store memory about location discovery
            await StoreMemoryAsync(new MemoryEntry
            {
                Type = MemoryType.LocationDiscovery,
                Importance = ImportanceLevel.Low,
                Content = $"{player.Name} moved to {targetLocation}",
                TurnNumber = _gameState.TurnNumber,
                LocationName = targetLocation,
                InvolvedEntities = new List<string> { player.Name }
            });
            
            Console.WriteLine($"\n✅ Moving to {targetLocation}...\n");
            return true;
        }
        
        Console.WriteLine($"\n❌ Can't go to '{action.Target}' from here.\n");
        return true;
    }
    
    /// <summary>
    /// Gets DM response to player action with memory enrichment
    /// </summary>
    private async Task<DMResponse?> GetDMResponseAsync(
        GameAction action, 
        PlayerCharacter player, 
        Location location)
    {
        // Build context message
        string contextMessage = BuildContextMessage(action, player, location);
        
        // Get enriched context with memories and relationships
        var relevantEntities = location.NPCs.Concat(_gameState.Players.Select(p => p.Name)).ToList();
        var enrichedContext = await _configuration.ContextManager.GetEnrichedContextAsync(
            "Dungeon Master",
            $"Player {player.Name} action: {action} at {location.Name}",
            relevantEntities,
            _configuration.RelationshipManager
        );
        
        contextMessage += $"\n\n{enrichedContext}";
        
        // Get conversation history with compression
        var conversationHistory = _configuration.ContextManager.GetConversationHistory(
            "Dungeon Master", 
            includeSummary: true);
        
        // Add current context
        var userMessage = new ChatMessage(ChatMessageRoles.User, contextMessage);
        conversationHistory.Add(userMessage);
        
        // Track message
        _configuration.ContextManager.AddMessage("Dungeon Master", userMessage);
        
        // Run DM agent
        Conversation conv = await _dungeonMaster.Run(appendMessages: conversationHistory);
        DMResponse? response = await conv.Messages.Last().Content?.SmartParseJsonAsync<DMResponse>(_dungeonMaster);
        
        if (response != null)
        {
            // Track DM response in conversation
            var assistantMessage = new ChatMessage(ChatMessageRoles.Assistant, response.Value.Narrative);
            _configuration.ContextManager.AddMessage("Dungeon Master", assistantMessage);
        }
        
        return response;
    }
    
    /// <summary>
    /// Builds the context message for the DM
    /// </summary>
    private string BuildContextMessage(GameAction action, PlayerCharacter player, Location location)
    {
        var adventureContext = _adventure != null
            ? $"""
            
            === ADVENTURE CONTEXT ===
            Current Quest: {GetCurrentQuestInfo()}
            Available Scenes: {GetNearbyScenes()}
            Potential Encounters: {GetPotentialEncounters()}
            """
            : "";

        return $"""
            Current Location: {location.Name}
            Players Present: {string.Join(", ", _gameState.Players.Select(p => $"{p.Name} (HP: {p.Health}/{p.MaxHealth})"))}
            Turn: {_gameState.TurnNumber}
            {adventureContext}
            
            Player Action: {action}
            """;
    }
    
    /// <summary>
    /// Displays DM narrative
    /// </summary>
    private void DisplayNarrative(string narrative)
    {
        Console.WriteLine("\n" + new string('─', 80));
        Console.WriteLine(narrative);
        Console.WriteLine(new string('─', 80) + "\n");
    }
    
    /// <summary>
    /// Stores interaction memory based on action type
    /// </summary>
    private async Task StoreInteractionMemoryAsync(
        GameAction action, 
        PlayerCharacter player, 
        DMResponse response, 
        Location location)
    {
        MemoryEntry? memory = null;
        
        if (action.Type == ActionType.Talk && !string.IsNullOrEmpty(action.Target))
        {
            // NPC interaction
            memory = new MemoryEntry
            {
                Type = MemoryType.PlayerNPCInteraction,
                Importance = ImportanceLevel.Medium,
                Content = $"{player.Name} talked to {action.Target} at {location.Name}: {response.Narrative}",
                TurnNumber = _gameState.TurnNumber,
                LocationName = location.Name,
                InvolvedEntities = new List<string> { player.Name, action.Target }
            };
            
            // Update relationship - talking is generally positive
            _configuration.RelationshipManager.UpdateRelationship(
                player.Name,
                action.Target,
                friendlinessChange: 5,
                trustChange: 2,
                note: $"Conversed at {location.Name}"
            );
        }
        else if (action.Type == ActionType.Examine || action.Type == ActionType.Search)
        {
            // Story/exploration memory
            memory = new MemoryEntry
            {
                Type = MemoryType.CriticalStory,
                Importance = ImportanceLevel.Low,
                Content = $"{player.Name} {action.Type} at {location.Name}: {response.Narrative}",
                TurnNumber = _gameState.TurnNumber,
                LocationName = location.Name,
                InvolvedEntities = new List<string> { player.Name }
            };
        }
        
        if (memory != null)
        {
            await StoreMemoryAsync(memory);
        }
    }
    
    /// <summary>
    /// Initiates combat with monsters from the adventure
    /// </summary>
    private async Task<PhaseResult> InitiateCombatAsync(
        DMResponse response, 
        PlayerCharacter player, 
        Location location)
    {
        List<object> monsters = new();
        bool isBoss = false;
        string encounterDescription = response.Narrative;

        // Try to get monsters from adventure
        if (_adventure != null)
        {
            // Find current scene by matching location name
            var currentScene = _adventure.Scenes.Values.FirstOrDefault(s => s.Name == location.Name);
            
            if (currentScene != null)
            {
                // Check if DM specified boss encounter by looking for boss in this scene
                var boss = _adventure.Bosses.FirstOrDefault(b => b.SceneId == currentScene.Id);
                
                // Boss encounter if: boss exists in scene AND (DM named it OR DM initiated combat in boss scene)
                if (boss != null && (
                    (response.NewQuestItems?.Any(name => name.Equals(boss.Name, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                    response.CombatInitiated)) // DM initiated combat in a boss scene
                {
                    // Boss encounter
                    monsters.Add(boss);
                    isBoss = true;
                    encounterDescription = $"{response.Narrative} - Boss Encounter: {boss.Name}";
                }
                else if (currentScene.EnemiesEncounters != null && currentScene.EnemiesEncounters.Any())
                {
                    // Regular monster encounter - get monsters from scene
                    var encounter = currentScene.EnemiesEncounters.FirstOrDefault();
                    if (encounter != null)
                    {
                        foreach (var monsterGroup in encounter.MonsterGroups)
                        {
                            // Add the monster multiple times based on quantity
                            for (int i = 0; i < monsterGroup.Quantity; i++)
                            {
                                monsters.Add(monsterGroup.Monster);
                            }
                        }
                        
                        if (monsters.Any())
                        {
                            var monstersNames = monsters.Cast<Monsters>().Select(m => m.Name).Distinct();
                            encounterDescription = $"{response.Narrative} - Encounter: {string.Join(", ", monstersNames)}";
                        }
                    }
                }
            }
        }

        // Fallback to default enemies if no adventure monsters found
        if (!monsters.Any())
        {
            var enemies = response.NewQuestItems ?? Array.Empty<string>();
            if (enemies.Length == 0)
            {
                enemies = new[] { "Goblin", "Wolf" }; // Default enemies
            }

            // Convert to default monsters for backward compatibility
            monsters = enemies.Select(name => new Monsters(name, 1, new MonsterStats
            {
                Health = new Random().Next(30, 60),
                AttackPower = new Random().Next(8, 15),
                Defense = new Random().Next(3, 8),
                AttackMovementRange = 2
            })).Cast<object>().ToList();

            encounterDescription = response.Narrative;
        }

        // Store combat event memory
        var monsterNames = monsters.Select(m => 
            m is Boss boss ? boss.Name : 
            m is Monsters mon ? mon.Name : 
            "Unknown").ToList();
        
        await StoreMemoryAsync(new MemoryEntry
        {
            Type = MemoryType.CombatEvent,
            Importance = ImportanceLevel.High,
            Content = $"Combat initiated with {string.Join(", ", monsterNames)} at {location.Name}",
            TurnNumber = _gameState.TurnNumber,
            LocationName = location.Name,
            InvolvedEntities = monsterNames.Concat(new[] { player.Name }).ToList()
        });
        
        await _combatManager.InitiateCombatAsync(monsters, isBoss, encounterDescription);
        Console.WriteLine("\n⚔️ Combat has begun!\n");
        
        return CreateResult(GamePhase.Combat, true);
    }
    
    /// <summary>
    /// Stores a memory entry
    /// </summary>
    private async Task StoreMemoryAsync(MemoryEntry memory)
    {
        try
        {
            await _configuration.MemoryManager.StoreMemoryAsync(memory);
        }
        catch (Exception ex)
        {
            // Log but don't interrupt gameplay
            Console.WriteLine($"⚠️ Memory storage warning: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets current quest information
    /// </summary>
    private string GetCurrentQuestInfo()
    {
        if (_adventure == null || _adventure.CurrentQuestIndex >= _adventure.MainQuestLine.Count)
        {
            return "No active quest";
        }

        var quest = _adventure.MainQuestLine[_adventure.CurrentQuestIndex];
        return $"{quest.Name} - {quest.Description}";
    }

    /// <summary>
    /// Gets nearby scenes from adventure
    /// </summary>
    private string GetNearbyScenes()
    {
        if (_adventure == null || !_adventure.Scenes.Any())
        {
            return "No scenes available";
        }

        return string.Join(", ", _adventure.Scenes.Values.Take(5).Select(s => s.Name));
    }

    /// <summary>
    /// Gets potential encounters from adventure
    /// </summary>
    private string GetPotentialEncounters()
    {
        if (_adventure == null)
        {
            return "Random encounters possible";
        }

        var encounters = new List<string>();
        
        // Check for trash mobs in current area
        var nearbyMobs = _adventure.Scenes[_gameState.CurrentLocationName].EnemiesEncounters.Take(3).Select(m => string.Join(",", m.MonsterGroups.Select(g=>g.Monster.Name)));
        encounters.AddRange(nearbyMobs);

        // Check for bosses
        var nearbyBosses = _adventure.Bosses.Take(2).Select(b => b.Name);
        encounters.AddRange(nearbyBosses);

        return encounters.Any() ? string.Join(", ", encounters) : "None specifically planned";
    }
    
    /// <summary>
    /// Creates a phase result
    /// </summary>
    private PhaseResult CreateResult(GamePhase phase, bool shouldContinue)
    {
        return new PhaseResult 
        { 
            CurrentPhase = phase, 
            ShouldContinue = shouldContinue 
        };
    }
}
