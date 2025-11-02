using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Common;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Manages combat phase logic
/// </summary>
public class CombatManager
{
    private readonly GameState _gameState;
    private readonly Random _random = new();
    private readonly CombatZoneGenerator? _zoneGenerator;

    public CombatManager(GameState gameState, TornadoApi? client = null)
    {
        _gameState = gameState;
        if (client != null)
        {
            _zoneGenerator = new CombatZoneGenerator(client);
        }
    }

    /// <summary>
    /// Initialize combat with enemies using AI-generated zone
    /// </summary>
    public async Task InitiateCombatAsync(List<string> enemyNames, string encounterDescription)
    {
        _gameState.CurrentPhase = GamePhase.Combat;
        _gameState.CombatState = new CombatState
        {
            IsActive = true
        };

        var currentLocation = _gameState.Locations[_gameState.CurrentLocationName];
        var playerNames = _gameState.Players.Where(p => p.Health > 0).Select(p => p.Name).ToList();

        CombatZoneSetup zoneSetup;

        // Use AI to generate combat zone if available
        if (_zoneGenerator != null)
        {
            try
            {
                Console.WriteLine("\n🎲 Generating tactical combat zone...");
                zoneSetup = await _zoneGenerator.GenerateCombatZoneAsync(
                    currentLocation.Name,
                    currentLocation.Description,
                    playerNames,
                    enemyNames,
                    encounterDescription);
                Console.WriteLine($"⚔️ Battlefield: {zoneSetup.Terrain}");
                if (!string.IsNullOrEmpty(zoneSetup.Description))
                {
                    Console.WriteLine($"📜 {zoneSetup.Description}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Combat zone generation failed, using basic setup: {ex.Message}");
                zoneSetup = CreateBasicZoneSetup(playerNames, enemyNames);
            }
        }
        else
        {
            zoneSetup = CreateBasicZoneSetup(playerNames, enemyNames);
        }

        // Apply the zone setup
        ApplyZoneSetup(zoneSetup, enemyNames);

        _gameState.GameHistory.Add($"Turn {_gameState.TurnNumber}: Combat initiated! {encounterDescription}");
    }

    /// <summary>
    /// Initialize combat with enemies (synchronous version for backward compatibility)
    /// </summary>
    public void InitiateCombat(List<string> enemyNames, string encounterDescription)
    {
        InitiateCombatAsync(enemyNames, encounterDescription).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Apply the zone setup to create combat entities and obstacles
    /// </summary>
    private void ApplyZoneSetup(CombatZoneSetup setup, List<string> enemyNames)
    {
        _gameState.CombatState!.GridWidth = setup.GridWidth;
        _gameState.CombatState.GridHeight = setup.GridHeight;
        _gameState.CombatState.Terrain = setup.Terrain;
        _gameState.CombatState.TerrainDescription = setup.Description;

        // Add players based on setup
        foreach (var playerSetup in setup.PlayerPositions)
        {
            var player = _gameState.Players.FirstOrDefault(p => p.Name == playerSetup.Name);
            if (player != null && player.Health > 0)
            {
                _gameState.CombatState.Entities.Add(new CombatEntity
                {
                    Name = player.Name,
                    Health = player.Health,
                    MaxHealth = player.MaxHealth,
                    Position = new GridPosition(playerSetup.X, playerSetup.Y),
                    IsPlayer = true,
                    AttackPower = player.Stats.GetValueOrDefault("Strength", 10),
                    Defense = player.Stats.GetValueOrDefault("Constitution", 10),
                    MovementRange = player.Stats.GetValueOrDefault("Dexterity", 10) / 3
                });
            }
        }

        // Add enemies based on setup
        for (int i = 0; i < enemyNames.Count && i < setup.EnemyPositions.Count; i++)
        {
            var enemySetup = setup.EnemyPositions[i];
            var enemyHealth = _random.Next(20, 50);
            _gameState.CombatState.Entities.Add(new CombatEntity
            {
                Name = enemyNames[i],
                Health = enemyHealth,
                MaxHealth = enemyHealth,
                Position = new GridPosition(enemySetup.X, enemySetup.Y),
                IsPlayer = false,
                AttackPower = _random.Next(8, 15),
                Defense = _random.Next(3, 8),
                MovementRange = 2
            });
        }

        // Add obstacles from setup
        foreach (var obstacle in setup.Obstacles)
        {
            _gameState.CombatState.Obstacles.Add(new GridPosition(obstacle.X, obstacle.Y));
        }
    }

    /// <summary>
    /// Create a basic zone setup without AI
    /// </summary>
    private CombatZoneSetup CreateBasicZoneSetup(List<string> playerNames, List<string> enemyNames)
    {
        var setup = new CombatZoneSetup
        {
            Terrain = "Open Ground",
            Description = "A simple battlefield",
            GridWidth = 10,
            GridHeight = 10,
            PlayerPositions = new List<EntitySetup>(),
            EnemyPositions = new List<EntitySetup>(),
            Obstacles = new List<ObstacleSetup>()
        };

        // Position players on the left
        for (int i = 0; i < playerNames.Count; i++)
        {
            setup.PlayerPositions.Add(new EntitySetup(playerNames[i], 1, 4 + i, "normal"));
        }

        // Position enemies on the right
        for (int i = 0; i < enemyNames.Count; i++)
        {
            setup.EnemyPositions.Add(new EntitySetup(enemyNames[i], 8, 4 + i, "normal"));
        }

        // Add some random obstacles
        for (int i = 0; i < 3; i++)
        {
            setup.Obstacles.Add(new ObstacleSetup(
                _random.Next(2, 8),
                _random.Next(2, 8),
                "obstacle",
                "Obstacle"));
        }

        return setup;
    }

    /// <summary>
    /// Process a combat action
    /// </summary>
    public string ProcessCombatAction(GameAction action)
    {
        if (_gameState.CombatState == null || !_gameState.CombatState.IsActive)
        {
            return "No active combat!";
        }

        var currentEntity = _gameState.CombatState.GetCurrentEntity();
        if (currentEntity == null)
        {
            return "No entity to act!";
        }

        string result = action.Type switch
        {
            ActionType.Attack => ProcessAttack(currentEntity, action.Target),
            ActionType.CombatMove => ProcessMove(currentEntity, action.Parameters),
            ActionType.UseItem => ProcessUseItem(currentEntity, action.Target),
            ActionType.Defend => ProcessDefend(currentEntity),
            ActionType.Retreat => ProcessRetreat(currentEntity),
            _ => "Invalid combat action!"
        };

        // Advance turn
        AdvanceTurn();

        return result;
    }

    private string ProcessAttack(CombatEntity attacker, string? targetName)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            return "No target specified!";
        }

        var target = _gameState.CombatState!.Entities.FirstOrDefault(e => 
            e.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) && !e.IsDefeated);

        if (target == null)
        {
            return $"Target {targetName} not found!";
        }

        // Check range
        if (attacker.Position.DistanceTo(target.Position) > 1)
        {
            return $"{attacker.Name} is too far from {target.Name} to attack!";
        }

        // Calculate damage
        int attackRoll = _random.Next(1, 21) + attacker.AttackPower;
        int defenseRoll = _random.Next(1, 21) + target.Defense;
        int damage = Math.Max(0, attackRoll - defenseRoll);

        target.Health -= damage;
        if (target.Health <= 0)
        {
            target.Health = 0;
            target.IsDefeated = true;
            
            // Update player health if defeated
            if (target.IsPlayer)
            {
                var player = _gameState.Players.FirstOrDefault(p => p.Name == target.Name);
                if (player != null)
                {
                    player.Health = 0;
                }
            }
            
            return $"{attacker.Name} attacks {target.Name}! [Roll: {attackRoll} vs {defenseRoll}] Deals {damage} damage! {target.Name} is defeated!";
        }

        return $"{attacker.Name} attacks {target.Name}! [Roll: {attackRoll} vs {defenseRoll}] Deals {damage} damage! ({target.Health}/{target.MaxHealth} HP remaining)";
    }

    private string ProcessMove(CombatEntity entity, Dictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("x", out var xStr) || !parameters.TryGetValue("y", out var yStr))
        {
            return "Invalid move parameters!";
        }

        if (!int.TryParse(xStr, out int x) || !int.TryParse(yStr, out int y))
        {
            return "Invalid coordinates!";
        }

        var newPos = new GridPosition(x, y);
        if (entity.Position.DistanceTo(newPos) > entity.MovementRange)
        {
            return $"Too far! {entity.Name} can only move {entity.MovementRange} spaces.";
        }

        // Check for obstacles
        if (_gameState.CombatState!.Obstacles.Any(o => o.X == x && o.Y == y))
        {
            return "Can't move there - obstacle!";
        }

        // Check for other entities
        if (_gameState.CombatState.Entities.Any(e => e.Position.X == x && e.Position.Y == y && !e.IsDefeated))
        {
            return "Space occupied!";
        }

        var oldPos = entity.Position;
        entity.Position = newPos;
        return $"{entity.Name} moves from {oldPos} to {newPos}";
    }

    private string ProcessUseItem(CombatEntity entity, string? itemName)
    {
        if (!entity.IsPlayer)
        {
            return "Only players can use items!";
        }

        var player = _gameState.Players.FirstOrDefault(p => p.Name == entity.Name);
        if (player == null || string.IsNullOrEmpty(itemName))
        {
            return "Invalid item use!";
        }

        if (!player.Inventory.Contains(itemName))
        {
            return $"{player.Name} doesn't have {itemName}!";
        }

        // Simple item effects
        if (itemName.Contains("Health Potion", StringComparison.OrdinalIgnoreCase))
        {
            int healing = 50;
            entity.Health = Math.Min(entity.MaxHealth, entity.Health + healing);
            player.Health = entity.Health;
            player.Inventory.Remove(itemName);
            return $"{entity.Name} uses {itemName} and restores {healing} HP! ({entity.Health}/{entity.MaxHealth})";
        }

        return $"{entity.Name} uses {itemName}";
    }

    private string ProcessDefend(CombatEntity entity)
    {
        entity.Defense += 5; // Temporary defense boost
        return $"{entity.Name} takes a defensive stance! (+5 defense this turn)";
    }

    private string ProcessRetreat(CombatEntity entity)
    {
        entity.IsRetreating = true;
        return $"{entity.Name} attempts to retreat from combat!";
    }

    private void AdvanceTurn()
    {
        if (_gameState.CombatState == null) return;

        _gameState.CombatState.CurrentEntityIndex++;
        
        // Skip defeated or retreating entities
        while (_gameState.CombatState.CurrentEntityIndex < _gameState.CombatState.Entities.Count)
        {
            var entity = _gameState.CombatState.GetCurrentEntity();
            if (entity != null && !entity.IsDefeated && !entity.IsRetreating)
            {
                break;
            }
            _gameState.CombatState.CurrentEntityIndex++;
        }

        // If we've gone through all entities, start new round
        if (_gameState.CombatState.CurrentEntityIndex >= _gameState.CombatState.Entities.Count)
        {
            _gameState.CombatState.CurrentEntityIndex = 0;
            _gameState.CombatState.CurrentTurn++;
            
            // Reset temporary effects
            foreach (var entity in _gameState.CombatState.Entities)
            {
                entity.Defense = entity.IsPlayer 
                    ? _gameState.Players.FirstOrDefault(p => p.Name == entity.Name)?.Stats.GetValueOrDefault("Constitution", 10) ?? 5
                    : _random.Next(3, 8);
            }
        }
    }

    /// <summary>
    /// End combat and return to adventuring phase
    /// </summary>
    public string EndCombat()
    {
        if (_gameState.CombatState == null)
        {
            return "No combat to end!";
        }

        bool playersWon = _gameState.CombatState.PlayersWon();
        
        // Sync player health back
        foreach (var entity in _gameState.CombatState.Entities.Where(e => e.IsPlayer))
        {
            var player = _gameState.Players.FirstOrDefault(p => p.Name == entity.Name);
            if (player != null)
            {
                player.Health = entity.Health;
            }
        }

        _gameState.CurrentPhase = GamePhase.Adventuring;
        _gameState.CombatState.IsActive = false;

        string result = playersWon 
            ? "Victory! The party has defeated their foes!"
            : "Defeat! The party has fallen or retreated.";

        _gameState.GameHistory.Add($"Turn {_gameState.TurnNumber}: Combat ended. {result}");
        
        return result;
    }

    /// <summary>
    /// Get visual representation of combat grid
    /// </summary>
    public string GetCombatDisplay()
    {
        if (_gameState.CombatState == null)
        {
            return "No active combat.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══ COMBAT GRID ═══");
        sb.AppendLine($"🌍 Terrain: {_gameState.CombatState.Terrain}");
        if (!string.IsNullOrEmpty(_gameState.CombatState.TerrainDescription))
        {
            sb.AppendLine($"📜 {_gameState.CombatState.TerrainDescription}");
        }
        sb.AppendLine($"Turn: {_gameState.CombatState.CurrentTurn + 1}");
        
        var current = _gameState.CombatState.GetCurrentEntity();
        if (current != null)
        {
            sb.AppendLine($"⚡ Current: {current.Name} ({current.Health}/{current.MaxHealth} HP) @ {current.Position}");
        }
        
        sb.AppendLine();

        for (int y = 0; y < _gameState.CombatState.GridHeight; y++)
        {
            for (int x = 0; x < _gameState.CombatState.GridWidth; x++)
            {
                var pos = new GridPosition(x, y);
                var entity = _gameState.CombatState.Entities.FirstOrDefault(e => 
                    e.Position.X == x && e.Position.Y == y && !e.IsDefeated);
                
                if (entity != null)
                {
                    sb.Append(entity.IsPlayer ? "P" : "E");
                }
                else if (_gameState.CombatState.Obstacles.Any(o => o.X == x && o.Y == y))
                {
                    sb.Append("#");
                }
                else
                {
                    sb.Append(".");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("\nLegend: P=Player, E=Enemy, #=Obstacle, .=Empty");
        sb.AppendLine("\nEntities:");
        foreach (var entity in _gameState.CombatState.Entities.Where(e => !e.IsDefeated))
        {
            sb.AppendLine($"  {(entity.IsPlayer ? "[P]" : "[E]")} {entity.Name}: {entity.Health}/{entity.MaxHealth} HP @ {entity.Position}");
        }

        return sb.ToString();
    }
}
