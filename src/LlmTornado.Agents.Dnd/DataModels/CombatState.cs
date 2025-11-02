namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents a position on the combat grid
/// </summary>
public struct GridPosition
{
    public int X { get; set; }
    public int Y { get; set; }

    public GridPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int DistanceTo(GridPosition other)
    {
        return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    }

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// Represents a combatant in the combat phase
/// </summary>
public class CombatEntity
{
    public string Name { get; set; } = string.Empty;
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public GridPosition Position { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsDefeated { get; set; }
    public bool IsRetreating { get; set; }
    public int AttackPower { get; set; } = 10;
    public int Defense { get; set; } = 5;
    public int MovementRange { get; set; } = 3;
}

/// <summary>
/// Represents the combat state
/// </summary>
public class CombatState
{
    public List<CombatEntity> Entities { get; set; } = new();
    public List<GridPosition> Obstacles { get; set; } = new();
    public string Terrain { get; set; } = "Open Ground";
    public string TerrainDescription { get; set; } = string.Empty;
    public int GridWidth { get; set; } = 10;
    public int GridHeight { get; set; } = 10;
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
        var playersAlive = Entities.Where(e => e.IsPlayer && !e.IsDefeated && !e.IsRetreating).Any();
        var enemiesAlive = Entities.Where(e => !e.IsPlayer && !e.IsDefeated && !e.IsRetreating).Any();
        return !playersAlive || !enemiesAlive;
    }

    public bool PlayersWon()
    {
        var playersAlive = Entities.Where(e => e.IsPlayer && !e.IsDefeated && !e.IsRetreating).Any();
        var enemiesAlive = Entities.Where(e => !e.IsPlayer && !e.IsDefeated && !e.IsRetreating).Any();
        return playersAlive && !enemiesAlive;
    }
}
