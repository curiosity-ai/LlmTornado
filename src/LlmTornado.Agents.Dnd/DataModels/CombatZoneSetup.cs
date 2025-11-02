namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents an AI-generated combat zone setup
/// </summary>
public struct CombatZoneSetup
{
    public string Terrain { get; set; }
    public string Description { get; set; }
    public int GridWidth { get; set; }
    public int GridHeight { get; set; }
    public List<EntitySetup> PlayerPositions { get; set; }
    public List<EntitySetup> EnemyPositions { get; set; }
    public List<ObstacleSetup> Obstacles { get; set; }
    public List<TerrainFeature> TerrainFeatures { get; set; }

    public CombatZoneSetup()
    {
        Terrain = "Open Ground";
        Description = string.Empty;
        GridWidth = 10;
        GridHeight = 10;
        PlayerPositions = new List<EntitySetup>();
        EnemyPositions = new List<EntitySetup>();
        Obstacles = new List<ObstacleSetup>();
        TerrainFeatures = new List<TerrainFeature>();
    }
}

/// <summary>
/// Setup information for positioning an entity
/// </summary>
public struct EntitySetup
{
    public string Name { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string StartingCondition { get; set; } // e.g., "hidden", "elevated", "prone"

    public EntitySetup(string name, int x, int y, string condition = "normal")
    {
        Name = name;
        X = x;
        Y = y;
        StartingCondition = condition;
    }
}

/// <summary>
/// Setup information for obstacles
/// </summary>
public struct ObstacleSetup
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Type { get; set; } // e.g., "rock", "tree", "wall", "pit"
    public string Description { get; set; }

    public ObstacleSetup(int x, int y, string type, string description)
    {
        X = x;
        Y = y;
        Type = type;
        Description = description;
    }
}

/// <summary>
/// Special terrain features that affect gameplay
/// </summary>
public struct TerrainFeature
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Type { get; set; } // e.g., "high_ground", "cover", "difficult_terrain"
    public string Effect { get; set; } // e.g., "+2 defense", "movement cost doubled"

    public TerrainFeature(int x, int y, string type, string effect)
    {
        X = x;
        Y = y;
        Type = type;
        Effect = effect;
    }
}
