namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents a complete generated adventure
/// </summary>
public class Adventure
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;
    public string Setting { get; set; } = string.Empty;
    public List<Quest> MainQuestLine { get; set; } = new();
    public List<Quest> SideQuests { get; set; } = new();
    public Dictionary<string, Scene> Scenes { get; set; } = new();
    public List<Boss> Bosses { get; set; } = new();
    public List<TrashMobGroup> TrashMobs { get; set; } = new();
    public List<RareEvent> RareEvents { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int CurrentQuestIndex { get; set; } = 0;
    public List<string> CompletedQuestIds { get; set; } = new();
}

/// <summary>
/// Difficulty levels for adventures
/// </summary>
public enum DifficultyLevel
{
    Easy,
    Medium,
    Hard,
    Deadly
}

/// <summary>
/// Represents a quest in the adventure
/// </summary>
public class Quest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public QuestType Type { get; set; } = QuestType.Main;
    public List<string> Requirements { get; set; } = new(); // Quest IDs that must be completed first
    public Dictionary<string, string> Rewards { get; set; } = new(); // e.g., "Gold": "500", "Item": "Magic Sword"
    public string StartEvent { get; set; } = string.Empty;
    public string CompletionRequirements { get; set; } = string.Empty;
    public string StartSceneId { get; set; } = string.Empty;
    public string CompletionSceneId { get; set; } = string.Empty;
    public int RecommendedLevel { get; set; } = 1;
    public bool IsCompleted { get; set; } = false;
}

/// <summary>
/// Quest types
/// </summary>
public enum QuestType
{
    Main,
    Side,
    Hidden
}

/// <summary>
/// Represents a scene/location in the adventure
/// </summary>
public class Scene
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int GridSize { get; set; } = 10;
    public GridScale Scale { get; set; } = GridScale.Small;
    public Dictionary<string, string> Exits { get; set; } = new(); // Direction -> SceneId
    public List<string> NPCs { get; set; } = new();
    public List<string> Items { get; set; } = new();
    public string Terrain { get; set; } = "Open Ground";
    public List<SceneEnemy> Enemies { get; set; } = new();
}

/// <summary>
/// Scale of the grid for time calculation
/// </summary>
public enum GridScale
{
    Small,      // 1 space = 5 feet (minutes)
    Medium,     // 1 space = 50 feet (tens of minutes)
    Large,      // 1 space = 500 feet (hours)
    Huge        // 1 space = 5000 feet (days)
}

/// <summary>
/// Enemy positioned in a scene
/// </summary>
public class SceneEnemy
{
    public string EnemyId { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>
/// Represents a boss enemy
/// </summary>
public class Boss
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public int GridX { get; set; }
    public int GridY { get; set; }
    public BossStats Stats { get; set; } = new();
    public List<string> TrashMobIds { get; set; } = new(); // Associated trash mobs
    public List<string> Abilities { get; set; } = new();
    public Dictionary<string, string> Loot { get; set; } = new();
}

/// <summary>
/// Boss statistics scaled by difficulty
/// </summary>
public class BossStats
{
    public int Health { get; set; }
    public int AttackPower { get; set; }
    public int Defense { get; set; }
    public int MovementRange { get; set; }
    public int Level { get; set; }
}

/// <summary>
/// Group of trash mob enemies
/// </summary>
public class TrashMobGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public List<TrashMob> Mobs { get; set; } = new();
    public int EncounterChance { get; set; } = 30; // Percentage
}

/// <summary>
/// Individual trash mob
/// </summary>
public class TrashMob
{
    public string Name { get; set; } = string.Empty;
    public int Health { get; set; }
    public int AttackPower { get; set; }
    public int Defense { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
}

/// <summary>
/// Represents a rare/special event
/// </summary>
public class RareEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public EventType Type { get; set; } = EventType.Loot;
    public int TriggerChance { get; set; } = 5; // Percentage
    public string TriggerCondition { get; set; } = string.Empty;
    public Dictionary<string, string> Rewards { get; set; } = new();
}

/// <summary>
/// Types of rare events
/// </summary>
public enum EventType
{
    Loot,
    HiddenBoss,
    SecretArea,
    SpecialNPC,
    Puzzle
}
