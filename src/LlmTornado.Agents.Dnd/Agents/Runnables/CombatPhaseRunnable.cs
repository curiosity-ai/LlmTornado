using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;

namespace LlmTornado.Agents.Dnd.Agents.Runnables;

/// <summary>
/// Handles the combat phase with turn-based tactical combat
/// </summary>
public class CombatPhaseRunnable : OrchestrationRunnable<PhaseResult, PhaseResult>
{
    private readonly GameState _gameState;
    private readonly CombatManager _combatManager;

    public CombatPhaseRunnable(Orchestration orchestrator, GameState gameState, CombatManager combatManager) 
        : base(orchestrator)
    {
        _gameState = gameState;
        _combatManager = combatManager;
    }

    public override ValueTask<PhaseResult> Invoke(RunnableProcess<PhaseResult, PhaseResult> process)
    {
        // Check if combat is still active
        if (!IsCombatActive())
        {
            return ValueTask.FromResult(CreateResult(GamePhase.Adventuring, true));
        }

        // Display combat state
        DisplayCombatGrid();

        // Get current combatant
        var currentEntity = _gameState.CombatState!.GetCurrentEntity();
        if (currentEntity == null)
        {
            return ValueTask.FromResult(CreateResult(GamePhase.Combat, true));
        }

        // Get action (player or AI)
        GameAction action = currentEntity.IsPlayer 
            ? GetPlayerAction(currentEntity)
            : GetAIAction(currentEntity);

        // Check for early exit
        if (action.Type == ActionType.Quit)
        {
            return ValueTask.FromResult(CreateResult(GamePhase.Combat, false));
        }

        // Handle special actions
        if (HandleSpecialAction(action, currentEntity))
        {
            return ValueTask.FromResult(CreateResult(GamePhase.Combat, true));
        }

        // Process combat action
        ProcessAction(action);

        return ValueTask.FromResult(CreateResult(GamePhase.Combat, true));
    }
    
    /// <summary>
    /// Checks if combat is active
    /// </summary>
    private bool IsCombatActive()
    {
        return _gameState.CombatState != null && _gameState.CombatState.IsActive;
    }
    
    /// <summary>
    /// Displays the combat grid
    /// </summary>
    private void DisplayCombatGrid()
    {
        Console.WriteLine(_combatManager.GetCombatDisplay());
    }
    
    /// <summary>
    /// Gets action from human player
    /// </summary>
    private GameAction GetPlayerAction(CombatEntity entity)
    {
        Console.WriteLine($"\n{entity.Name}'s turn!");
        Console.WriteLine(ActionParser.GetAvailableCommands(GamePhase.Combat));
        Console.Write("> ");
        
        string? input = Console.ReadLine();
        return ActionParser.ParseAction(input ?? "", entity.Name, GamePhase.Combat);
    }
    
    /// <summary>
    /// Gets action from AI enemy using simple tactical AI
    /// </summary>
    private GameAction GetAIAction(CombatEntity currentEntity)
    {
        var players = _gameState.CombatState!.Entities
            .Where(e => e.IsPlayer && !e.IsDefeated)
            .ToList();
            
        if (!players.Any())
        {
            return CreateDefendAction(currentEntity);
        }

        var nearestPlayer = players
            .OrderBy(p => currentEntity.Position.DistanceTo(p.Position))
            .First();
        
        // Attack if adjacent, otherwise move closer
        return currentEntity.Position.DistanceTo(nearestPlayer.Position) <= 1
            ? CreateAttackAction(currentEntity, nearestPlayer)
            : CreateMoveAction(currentEntity, nearestPlayer);
    }
    
    /// <summary>
    /// Creates an attack action
    /// </summary>
    private GameAction CreateAttackAction(CombatEntity attacker, CombatEntity target)
    {
        return new GameAction
        {
            Type = ActionType.Attack,
            Target = target.Name,
            PlayerName = attacker.Name,
            Description = $"Attack {target.Name}"
        };
    }
    
    /// <summary>
    /// Creates a move action toward a target
    /// </summary>
    private GameAction CreateMoveAction(CombatEntity entity, CombatEntity target)
    {
        int newX = entity.Position.X;
        int newY = entity.Position.Y;
        
        if (entity.Position.X < target.Position.X) newX++;
        else if (entity.Position.X > target.Position.X) newX--;
        
        if (entity.Position.Y < target.Position.Y) newY++;
        else if (entity.Position.Y > target.Position.Y) newY--;
        
        return new GameAction
        {
            Type = ActionType.CombatMove,
            PlayerName = entity.Name,
            Description = "Move closer",
            Parameters = new Dictionary<string, string>
            {
                { "x", newX.ToString() },
                { "y", newY.ToString() }
            }
        };
    }
    
    /// <summary>
    /// Creates a defend action
    /// </summary>
    private GameAction CreateDefendAction(CombatEntity entity)
    {
        return new GameAction
        {
            Type = ActionType.Defend,
            PlayerName = entity.Name,
            Description = "Defend"
        };
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
        Console.WriteLine($"Position: {entity.Position} | Attack: {entity.AttackPower} | Defense: {entity.Defense}");
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
