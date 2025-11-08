using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Agents.Dnd.Agents.Runnables;

/// <summary>
/// Handles the combat phase with simple turn-based combat
/// </summary>
public class CombatPhaseRunnable : OrchestrationRunnable<PhaseResult, PhaseResult>
{
    private readonly GameState _gameState;
    private readonly CombatManager _combatManager;
    private readonly TornadoAgent _dungeonMaster;
    private readonly ImprovedDndGameConfiguration _configuration;

    public CombatPhaseRunnable(Orchestration orchestrator, GameState gameState, CombatManager combatManager) 
        : base(orchestrator)
    {
        _gameState = gameState;
        _combatManager = combatManager;
        _configuration = (ImprovedDndGameConfiguration)orchestrator;

        // Create DM agent for controlling enemy actions
        string combatDMInstructions = BuildCombatDMInstructions();
        _dungeonMaster = new TornadoAgent(
            client: _configuration.Client,
            model: ChatModel.OpenAi.Gpt4.O,
            name: "Dungeon Master (Combat)",
            instructions: combatDMInstructions,
            outputSchema: typeof(CombatEnemyAction));
    }

    public override async ValueTask<PhaseResult> Invoke(RunnableProcess<PhaseResult, PhaseResult> process)
    {
        process.RegisterAgent(agent: _dungeonMaster);

        // Check if combat is still active
        if (!IsCombatActive())
        {
            return CreateResult(GamePhase.Adventuring, true);
        }

        // Check if combat is over
        if (_gameState.CombatState!.IsCombatOver())
        {
            string result = _combatManager.EndCombat();
            Console.WriteLine($"\n{result}\n");
            return CreateResult(GamePhase.Adventuring, true);
        }

        // Display combat state
        DisplayCombatStatus();

        // Get current combatant
        var currentEntity = _gameState.CombatState!.GetCurrentEntity();
        if (currentEntity == null)
        {
            return CreateResult(GamePhase.Combat, true);
        }

        // Get action (player or DM-controlled enemy)
        GameAction action = currentEntity.IsPlayer 
            ? GetPlayerAction(currentEntity)
            : await GetDMEnemyActionAsync(currentEntity);

        // Check for early exit
        if (action.Type == ActionType.Quit)
        {
            return CreateResult(GamePhase.Combat, false);
        }

        // Handle special actions
        if (HandleSpecialAction(action, currentEntity))
        {
            return CreateResult(GamePhase.Combat, true);
        }

        // Process combat action
        ProcessAction(action);

        // Check again if combat is over after the action
        if (_gameState.CombatState.IsCombatOver())
        {
            string result = _combatManager.EndCombat();
            Console.WriteLine($"\n{result}\n");
            return CreateResult(GamePhase.Adventuring, true);
        }

        return CreateResult(GamePhase.Combat, true);
    }
    
    /// <summary>
    /// Checks if combat is active
    /// </summary>
    private bool IsCombatActive()
    {
        return _gameState.CombatState != null && _gameState.CombatState.IsActive;
    }
    
    /// <summary>
    /// Displays the combat status
    /// </summary>
    private void DisplayCombatStatus()
    {
        Console.WriteLine(_combatManager.GetCombatDisplay());
    }
    
    /// <summary>
    /// Gets action from human player
    /// </summary>
    private GameAction GetPlayerAction(CombatEntity entity)
    {
        Console.WriteLine($"\n⚔️ {entity.Name}'s turn!");
        Console.WriteLine(ActionParser.GetAvailableCommands(GamePhase.Combat));
        Console.Write("> ");
        
        string? input = Console.ReadLine();
        return ActionParser.ParseAction(input ?? "", entity.Name, GamePhase.Combat);
    }
    
    /// <summary>
    /// Gets action from DM for enemy turn
    /// </summary>
    private async Task<GameAction> GetDMEnemyActionAsync(CombatEntity enemy)
    {
        // Build combat context for DM
        var alivePlayers = _gameState.CombatState!.Entities
            .Where(e => e.IsPlayer && !e.IsDefeated)
            .ToList();
        
        var aliveEnemies = _gameState.CombatState.Entities
            .Where(e => !e.IsPlayer && !e.IsDefeated)
            .ToList();

        string contextMessage = BuildCombatContext(enemy, alivePlayers, aliveEnemies);

        // Get DM's decision for enemy action
        var conversationHistory = _configuration.ContextManager.GetConversationHistory(
            "Dungeon Master (Combat)", 
            includeSummary: true);
        
        var userMessage = new ChatMessage(ChatMessageRoles.User, contextMessage);
        conversationHistory.Add(userMessage);
        
        _configuration.ContextManager.AddMessage("Dungeon Master (Combat)", userMessage);

        // Run DM agent
        Conversation conv = await _dungeonMaster.Run(appendMessages: conversationHistory);
        CombatEnemyAction? enemyAction = await conv.Messages.Last().Content?.SmartParseJsonAsync<CombatEnemyAction>(_dungeonMaster);

        if (enemyAction != null)
        {
            // Track DM response
            var assistantMessage = new ChatMessage(ChatMessageRoles.Assistant, enemyAction.Value.Description);
            _configuration.ContextManager.AddMessage("Dungeon Master (Combat)", assistantMessage);

            // Convert DM action to GameAction
            return ConvertDMActionToGameAction(enemyAction.Value, enemy, alivePlayers);
        }

        // Fallback: attack first available player
        if (alivePlayers.Any())
        {
            return new GameAction
            {
                Type = ActionType.Attack,
                Target = alivePlayers.First().Name,
                PlayerName = enemy.Name,
                Description = $"{enemy.Name} attacks {alivePlayers.First().Name}"
            };
        }

        return new GameAction
        {
            Type = ActionType.Defend,
            PlayerName = enemy.Name,
            Description = $"{enemy.Name} defends"
        };
    }

    /// <summary>
    /// Builds combat context message for DM
    /// </summary>
    private string BuildCombatContext(CombatEntity enemy, List<CombatEntity> players, List<CombatEntity> enemies)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"It is {enemy.Name}'s turn in combat.");
        sb.AppendLine($"\n{enemy.Name} Status:");
        sb.AppendLine($"  Health: {enemy.Health}/{enemy.MaxHealth} HP");
        sb.AppendLine($"  Stats: {string.Join(", ", enemy.Stats.Select(s => $"{s.Key}: {s.Value}"))}");

        sb.AppendLine("\nAvailable Targets (Players):");
        foreach (var player in players)
        {
            sb.AppendLine($"  - {player.Name}: {player.Health}/{player.MaxHealth} HP");
            var playerStats = _gameState.Players.FirstOrDefault(p => p.Name == player.Name);
            if (playerStats != null)
            {
                sb.AppendLine($"    Stats: {string.Join(", ", playerStats.Stats.Select(s => $"{s.Key}: {s.Value}"))}");
            }
        }

        sb.AppendLine("\nOther Enemies:");
        foreach (var e in enemies.Where(e => e.Name != enemy.Name))
        {
            sb.AppendLine($"  - {e.Name}: {e.Health}/{e.MaxHealth} HP");
        }

        sb.AppendLine("\nWhat action should this enemy take? Choose: Attack [target], Defend, or describe a special action.");
        
        return sb.ToString();
    }

    /// <summary>
    /// Converts DM's enemy action to GameAction
    /// </summary>
    private GameAction ConvertDMActionToGameAction(CombatEnemyAction dmAction, CombatEntity enemy, List<CombatEntity> availableTargets)
    {
        var actionType = dmAction.ActionType?.ToLower() ?? "attack";
        
        if (actionType.Contains("attack") || actionType.Contains("strike") || actionType.Contains("hit"))
        {
            // Find target
            string? targetName = null;
            if (!string.IsNullOrEmpty(dmAction.Target))
            {
                targetName = availableTargets.FirstOrDefault(p => 
                    p.Name.Equals(dmAction.Target, StringComparison.OrdinalIgnoreCase))?.Name;
            }
            
            // If target not found or not specified, pick first available
            if (string.IsNullOrEmpty(targetName) && availableTargets.Any())
            {
                targetName = availableTargets.First().Name;
            }

            return new GameAction
            {
                Type = ActionType.Attack,
                Target = targetName ?? "Unknown",
                PlayerName = enemy.Name,
                Description = dmAction.Description ?? $"{enemy.Name} attacks {targetName}"
            };
        }

        // Default to defend
        return new GameAction
        {
            Type = ActionType.Defend,
            PlayerName = enemy.Name,
            Description = dmAction.Description ?? $"{enemy.Name} defends"
        };
    }
    
    /// <summary>
    /// Builds DM instructions for combat
    /// </summary>
    private string BuildCombatDMInstructions()
    {
        return """
            You are the Dungeon Master controlling enemy actions during combat.
            
            Your role:
            - Control enemy creatures during their combat turns
            - Decide what actions enemies take (attack, defend, special abilities)
            - Make tactical decisions based on the combat situation
            - Describe enemy actions with narrative flair
            - Act on behalf of the enemies - you ARE the enemy during their turns
            
            Combat Rules:
            - Enemies can Attack a player target (specify target name)
            - Enemies can Defend to reduce incoming damage
            - Enemies should make tactical decisions (attack weakest, focus fire, etc.)
            
            Response Format:
            - ActionType: "attack" or "defend"
            - Target: Name of player to attack (if attacking)
            - Description: Brief narrative description of the enemy's action
            
            Be creative but stay within the combat rules. Make enemies feel alive and tactical!
            """;
    }
    
    /// <summary>
    /// Handles special non-combat actions (inventory, status)
    /// </summary>
    private bool HandleSpecialAction(GameAction action, CombatEntity entity)
    {
        if (action.Type == ActionType.ViewInventory)
        {
            ShowInventory(entity);
            return true;
        }

        if (action.Type == ActionType.ViewStatus)
        {
            ShowStatus(entity);
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// Shows entity inventory
    /// </summary>
    private void ShowInventory(CombatEntity entity)
    {
        var player = _gameState.Players.FirstOrDefault(p => p.Name == entity.Name);
        if (player != null)
        {
            Console.WriteLine($"\n💼 Inventory: {string.Join(", ", player.Inventory)}");
        }
    }
    
    /// <summary>
    /// Shows entity status
    /// </summary>
    private void ShowStatus(CombatEntity entity)
    {
        Console.WriteLine($"\n📊 {entity.Name}: {entity.Health}/{entity.MaxHealth} HP");
        if (entity.Stats.Any())
        {
            Console.WriteLine($"Stats: {string.Join(", ", entity.Stats.Select(s => $"{s.Key}: {s.Value}"))}");
        }
    }
    
    /// <summary>
    /// Processes the combat action and updates game state
    /// </summary>
    private void ProcessAction(GameAction action)
    {
        string result = _combatManager.ProcessCombatAction(action);
        Console.WriteLine($"\n⚔️ {result}\n");
        
        _gameState.GameHistory.Add($"Turn {++_gameState.TurnNumber}: {action}");
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

/// <summary>
/// Represents an enemy action decided by the DM
/// </summary>
public struct CombatEnemyAction
{
    public string ActionType { get; set; }
    public string? Target { get; set; }
    public string Description { get; set; }
}