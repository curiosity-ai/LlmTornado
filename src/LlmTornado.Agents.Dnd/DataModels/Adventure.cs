using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

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
[JsonConverter(typeof(StringEnumConverter))]
public enum DifficultyLevel
{
    Easy,
    Medium,
    Hard,
    Deadly
}

public struct QuestReward
{
    [Description("Type of reward  e.g., Gold, Item, Experience")]
    public string Type { get; set; } = string.Empty; // e.g., "Gold", "Item", "Experience"

    [Description("Value of the reward e.g., amount of gold, item name, experience points (500, Magic Sword, 2000 XP)")]
    public string Value { get; set; } = string.Empty; // e.g., "500", "Magic Sword", "2000 XP"

    public QuestReward(string type, string value)
    {
        Type = type;
        Value = value;
    }
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
    public string[] Requirements { get; set; } = Array.Empty<string>(); // Quest IDs that must be completed first
    public QuestReward[] Rewards { get; set; } = Array.Empty<QuestReward>(); // e.g., "Gold": "500", "Item": "Magic Sword"
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
    public Exits[] Exits { get; set; } = Array.Empty<Exits>(); // Direction -> SceneId
    public List<string> NPCs { get; set; } = new();
    public List<string> Items { get; set; } = new();
    public string Terrain { get; set; } = "Open Ground";
    public List<SceneEnemy> Enemies { get; set; } = new();
}


public struct Exits
{
    public string Name { get; set; }
    public string SceneId { get; set; }
    public Exits(string name, string sceneId)
    {
        Name = name;
        SceneId = sceneId;
    }
}

/// <summary>
/// Scale of the grid for time calculation
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
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
    public BossLoot[] Loot { get; set; } = Array.Empty<BossLoot>();
}

public struct  BossLoot
{
    public string Name { get; set; }
    public int Quantity { get; set; }
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
    public EventReward[] Rewards { get; set; } = Array.Empty<EventReward>();
}

public struct EventReward
{
    public string Type { get; set; }
    public string Value { get; set; }
    public EventReward(string type, string value)
    {
        Type = type;
        Value = value;
    }
}

/// <summary>
/// Types of rare events
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum EventType
{
    Loot,
    HiddenBoss,
    SecretArea,
    SpecialNPC,
    Puzzle
}
