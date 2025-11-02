namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Types of memory entries that can be stored
/// </summary>
public enum MemoryType
{
    PlayerPlayerInteraction,
    PlayerNPCInteraction,
    CriticalStory,
    CombatEvent,
    QuestProgress,
    LocationDiscovery,
    ItemTransaction,
    RelationshipChange
}

/// <summary>
/// Importance level for memory prioritization
/// </summary>
public enum ImportanceLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Represents a memory entry that can be stored in vector database
/// </summary>
public class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public MemoryType Type { get; set; }
    public ImportanceLevel Importance { get; set; }
    public string Content { get; set; } = "";
    public string SessionId { get; set; } = "";
    public int TurnNumber { get; set; }
    
    // Entities involved
    public List<string> InvolvedEntities { get; set; } = new();
    
    // Location context
    public string? LocationName { get; set; }
    
    // Additional metadata
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Tracks relationship and friendliness between entities
/// </summary>
public class EntityRelationship
{
    public string Entity1Id { get; set; } = "";
    public string Entity2Id { get; set; } = "";
    public string Entity1Name { get; set; } = "";
    public string Entity2Name { get; set; } = "";
    
    /// <summary>
    /// Friendliness score: -100 (hostile) to 100 (friendly), 0 is neutral
    /// </summary>
    public int FriendlinessScore { get; set; } = 0;
    
    /// <summary>
    /// Trust level: 0 (no trust) to 100 (complete trust)
    /// </summary>
    public int TrustLevel { get; set; } = 50;
    
    /// <summary>
    /// Number of interactions between entities
    /// </summary>
    public int InteractionCount { get; set; } = 0;
    
    /// <summary>
    /// Last interaction timestamp
    /// </summary>
    public DateTime LastInteraction { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Notes about the relationship
    /// </summary>
    public List<string> RelationshipNotes { get; set; } = new();
    
    /// <summary>
    /// Relationship status tags
    /// </summary>
    public List<string> Tags { get; set; } = new(); // e.g., "ally", "enemy", "rival", "friend", "neutral"
}

/// <summary>
/// Compressed conversation summary for context management
/// </summary>
public class ConversationSummary
{
    public string AgentName { get; set; } = "";
    public string SessionId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; set; }
    public int TurnRange { get; set; } // Last N turns covered
    public string Summary { get; set; } = "";
    public List<string> KeyPoints { get; set; } = new();
    public List<string> ImportantDecisions { get; set; } = new();
}
