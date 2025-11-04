using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Manages combat phase logic with simple turn-based combat
/// </summary>
public class CombatManager
{
    private readonly GameState _gameState;
    private readonly Random _random = new();

    public CombatManager(GameState gameState)
    {
        _gameState = gameState;
    }

    /// <summary>
    /// Initialize combat with enemies from adventure - roll initiative and determine turn order
    /// </summary>
    public async Task InitiateCombatAsync(List<object> monsters, bool isBoss, string encounterDescription)
    {
        _gameState.CurrentPhase = GamePhase.Combat;
        _gameState.CombatState = new CombatState
        {
            IsActive = true,
            CurrentTurn = 1,
            CurrentEntityIndex = 0
        };

        // Add all players to combat
        foreach (var player in _gameState.Players.Where(p => p.Health > 0))
        {
            var entity = new CombatEntity
            {
                Name = player.Name,
                Health = player.Health,
                MaxHealth = player.MaxHealth,
                IsPlayer = true,
                Stats = new Dictionary<string, int>(player.Stats)
            };
            
            // Roll initiative: d20 + Dexterity modifier
            int dexModifier = GetStatModifier(player.Stats.GetValueOrDefault("Dexterity", 10));
            entity.Initiative = RollD20() + dexModifier;
            
            _gameState.CombatState.Entities.Add(entity);
        }

        // Add monsters from adventure to combat
        foreach (var monster in monsters)
        {
            CombatEntity entity;
            
            if (isBoss && monster is Boss boss)
            {
                // Handle Boss monster
                var stats = DeriveDndStatsFromMonsterStats(boss.Stats, 5); // Bosses are typically level 5+
                entity = new CombatEntity
                {
                    Name = boss.Name,
                    Health = boss.Stats.Health,
                    MaxHealth = boss.Stats.Health,
                    IsPlayer = false,
                    Stats = stats
                };
            }
            else if (monster is Monsters regularMonster)
            {
                // Handle regular Monster
                var stats = DeriveDndStatsFromMonsterStats(regularMonster.Stats, regularMonster.Level);
                entity = new CombatEntity
                {
                    Name = regularMonster.Name,
                    Health = regularMonster.Stats.Health,
                    MaxHealth = regularMonster.Stats.Health,
                    IsPlayer = false,
                    Stats = stats
                };
            }
            else
            {
                // Fallback for unknown types - skip
                continue;
            }
            
            // Roll initiative: d20 + Dexterity modifier
            int dexModifier = GetStatModifier(entity.Stats.GetValueOrDefault("Dexterity", 10));
            entity.Initiative = RollD20() + dexModifier;
            
            _gameState.CombatState.Entities.Add(entity);
        }

        // Sort entities by initiative (highest first)
        _gameState.CombatState.Entities = _gameState.CombatState.Entities
            .OrderByDescending(e => e.Initiative)
            .ToList();

        // Display initiative order
        Console.WriteLine("\n⚔️ Combat Initiated!");
        Console.WriteLine($"📜 {encounterDescription}");
        Console.WriteLine("\n🎲 Initiative Order:");
        for (int i = 0; i < _gameState.CombatState.Entities.Count; i++)
        {
            var entity = _gameState.CombatState.Entities[i];
            Console.WriteLine($"  {i + 1}. {entity.Name} (Initiative: {entity.Initiative})");
        }
        Console.WriteLine();

        _gameState.GameHistory.Add($"Turn {_gameState.TurnNumber}: Combat initiated! {encounterDescription}");
    }

    /// <summary>
    /// Initialize combat with enemies (backward compatibility - fallback to default enemies)
    /// </summary>
    public async Task InitiateCombatAsync(List<string> enemyNames, string encounterDescription)
    {
        // Convert to default monsters for backward compatibility
        var defaultMonsters = enemyNames.Select(name => new Monsters(name, 1, new MonsterStats
        {
            Health = _random.Next(30, 60),
            AttackPower = _random.Next(8, 15),
            Defense = _random.Next(3, 8),
            AttackMovementRange = 2
        })).Cast<object>().ToList();

        await InitiateCombatAsync(defaultMonsters, false, encounterDescription);
    }

    /// <summary>
    /// Roll a d20 dice
    /// </summary>
    private int RollD20()
    {
        return _random.Next(1, 21);
    }

    /// <summary>
    /// Roll a d6 dice (for damage)
    /// </summary>
    private int RollD6()
    {
        return _random.Next(1, 7);
    }

    /// <summary>
    /// Calculate stat modifier (D&D style: (stat - 10) / 2, rounded down)
    /// </summary>
    private int GetStatModifier(int statValue)
    {
        return (statValue - 10) / 2;
    }

    /// <summary>
    /// Derives D&D stats from MonsterStats following default D&D rules
    /// </summary>
    private Dictionary<string, int> DeriveDndStatsFromMonsterStats(MonsterStats monsterStats, int level)
    {
        // Strength: Derived from AttackPower (monster's combat effectiveness)
        // AttackPower typically ranges 8-20+, map to Strength 10-18
        int strength = Math.Clamp((monsterStats.AttackPower / 2) + 10, 8, 20);

        // Constitution: Derived from Defense (monster's durability)
        // Defense typically ranges 3-15+, map to Constitution 10-18
        int constitution = Math.Clamp((monsterStats.Defense / 2) + 10, 8, 20);

        // Dexterity: Based on monster level (higher level = higher dex for initiative)
        // Level 1-2: 10-12, Level 3-5: 12-14, Level 6+: 14-16
        int dexterity = level switch
        {
            <= 2 => _random.Next(10, 13),
            <= 5 => _random.Next(12, 15),
            _ => _random.Next(14, 17)
        };

        // Intelligence, Wisdom, Charisma: Set reasonable defaults based on monster type
        // Most monsters have lower mental stats (8-12 range)
        int intelligence = _random.Next(8, 13);
        int wisdom = _random.Next(8, 13);
        int charisma = _random.Next(6, 11);

        return new Dictionary<string, int>
        {
            { "Strength", strength },
            { "Dexterity", dexterity },
            { "Constitution", constitution },
            { "Intelligence", intelligence },
            { "Wisdom", wisdom },
            { "Charisma", charisma }
        };
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
            ActionType.UseItem => ProcessUseItem(currentEntity, action.Target),
            ActionType.Defend => ProcessDefend(currentEntity),
            _ => "Invalid combat action!"
        };

        // Advance turn
        AdvanceTurn();

        return result;
    }

    /// <summary>
    /// Process an attack action
    /// </summary>
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

        // Attack roll: d20 + Strength modifier (for melee)
        int attackRoll = RollD20();
        int strengthModifier = GetStatModifier(attacker.Stats.GetValueOrDefault("Strength", 10));
        int totalAttack = attackRoll + strengthModifier;

        // Defense: AC (Armor Class) = 10 + Constitution modifier (simplified)
        int constitutionModifier = GetStatModifier(target.Stats.GetValueOrDefault("Constitution", 10));
        int armorClass = 10 + constitutionModifier;

        string result = $"{attacker.Name} attacks {target.Name}! ";
        result += $"[Roll: {attackRoll} + {strengthModifier} = {totalAttack} vs AC {armorClass}] ";

        if (totalAttack >= armorClass)
        {
            // Hit! Calculate damage: d6 + Strength modifier
            int damageRoll = RollD6();
            int damageModifier = Math.Max(0, strengthModifier); // Damage can't be negative
            int totalDamage = damageRoll + damageModifier;

            target.Health -= totalDamage;
            
            result += $"HIT! Deals {damageRoll} + {damageModifier} = {totalDamage} damage! ";

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
                
                result += $"{target.Name} is defeated!";
            }
            else
            {
                result += $"{target.Name} has {target.Health}/{target.MaxHealth} HP remaining.";
            }
        }
        else
        {
            result += "MISS!";
        }

        return result;
    }

    /// <summary>
    /// Process using an item
    /// </summary>
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
        if (itemName.Contains("Health Potion", StringComparison.OrdinalIgnoreCase) || 
            itemName.Contains("Potion", StringComparison.OrdinalIgnoreCase))
        {
            int healing = 30;
            entity.Health = Math.Min(entity.MaxHealth, entity.Health + healing);
            player.Health = entity.Health;
            player.Inventory.Remove(itemName);
            return $"{entity.Name} uses {itemName} and restores {healing} HP! ({entity.Health}/{entity.MaxHealth})";
        }

        return $"{entity.Name} uses {itemName}";
    }

    /// <summary>
    /// Process defend action (gain temporary AC bonus)
    /// </summary>
    private string ProcessDefend(CombatEntity entity)
    {
        // Defense provides +2 AC for the rest of the round (simplified - just a message for now)
        return $"{entity.Name} takes a defensive stance! (+2 AC this round)";
    }

    /// <summary>
    /// Advance to the next combatant's turn
    /// </summary>
    private void AdvanceTurn()
    {
        if (_gameState.CombatState == null) return;

        _gameState.CombatState.CurrentEntityIndex++;
        
        // Skip defeated entities
        while (_gameState.CombatState.CurrentEntityIndex < _gameState.CombatState.Entities.Count)
        {
            var entity = _gameState.CombatState.GetCurrentEntity();
            if (entity != null && !entity.IsDefeated)
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
            
            // Skip defeated entities at start of new round
            while (_gameState.CombatState.CurrentEntityIndex < _gameState.CombatState.Entities.Count)
            {
                var entity = _gameState.CombatState.GetCurrentEntity();
                if (entity != null && !entity.IsDefeated)
                {
                    break;
                }
                _gameState.CombatState.CurrentEntityIndex++;
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
            : "Defeat! The party has fallen.";

        _gameState.GameHistory.Add($"Turn {_gameState.TurnNumber}: Combat ended. {result}");
        
        return result;
    }

    /// <summary>
    /// Get simple combat display
    /// </summary>
    public string GetCombatDisplay()
    {
        if (_gameState.CombatState == null)
        {
            return "No active combat.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══ COMBAT ═══");
        sb.AppendLine($"Round: {_gameState.CombatState.CurrentTurn}");
        
        var current = _gameState.CombatState.GetCurrentEntity();
        if (current != null)
        {
            sb.AppendLine($"⚡ Current Turn: {current.Name} (Initiative: {current.Initiative})");
        }
        
        sb.AppendLine("\n📊 Combatants:");
        
        // Players
        sb.AppendLine("  [Players]");
        foreach (var entity in _gameState.CombatState.Entities.Where(e => e.IsPlayer))
        {
            string status = entity.IsDefeated ? "💀 DEFEATED" : $"❤️ {entity.Health}/{entity.MaxHealth} HP";
            sb.AppendLine($"    {entity.Name}: {status}");
        }
        
        // Enemies
        sb.AppendLine("  [Enemies]");
        foreach (var entity in _gameState.CombatState.Entities.Where(e => !e.IsPlayer))
        {
            string status = entity.IsDefeated ? "💀 DEFEATED" : $"❤️ {entity.Health}/{entity.MaxHealth} HP";
            sb.AppendLine($"    {entity.Name}: {status}");
        }

        return sb.ToString();
    }
}