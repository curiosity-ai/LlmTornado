namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents a combatant in the combat phase
/// </summary>
public class CombatEntity
{
    public string Name { get; set; } = string.Empty;
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsDefeated { get; set; }
    public int Initiative { get; set; } = 0;
    public Dictionary<string, int> Stats { get; set; } = new();
}

/// <summary>
/// Represents the combat state
/// </summary>
public class CombatState
{
    public List<CombatEntity> Entities { get; set; } = new();
    public int CurrentTurn { get; set; } = 0;
    public int CurrentEntityIndex { get; set; } = 0;
    public bool IsActive { get; set; } = false;

    public CombatEntity? GetCurrentEntity()
    {
        if (CurrentEntityIndex >= 0 && CurrentEntityIndex < Entities.Count)
        {
            return Entities[CurrentEntityIndex];
        }
        return null;
    }

    public bool IsCombatOver()
    {
        var playersAlive = Entities.Where(e => e.IsPlayer && !e.IsDefeated).Any();
        var enemiesAlive = Entities.Where(e => !e.IsPlayer && !e.IsDefeated).Any();
        return !playersAlive || !enemiesAlive;
    }

    public bool PlayersWon()
    {
        var playersAlive = Entities.Where(e => e.IsPlayer && !e.IsDefeated).Any();
        var enemiesAlive = Entities.Where(e => !e.IsPlayer && !e.IsDefeated).Any();
        return playersAlive && !enemiesAlive;
    }
}
