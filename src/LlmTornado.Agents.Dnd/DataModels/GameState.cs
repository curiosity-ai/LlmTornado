namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents the complete game state
/// </summary>
public class GameState
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public string CurrentLocationName { get; set; } = "Tavern";
    public List<PlayerCharacter> Players { get; set; } = new();
    public Dictionary<string, Location> Locations { get; set; } = new();
    public List<string> QuestLog { get; set; } = new();
    public List<string> GameHistory { get; set; } = new();
    public int TurnNumber { get; set; } = 0;
    public GamePhase CurrentPhase { get; set; } = GamePhase.Adventuring;
    public CombatState? CombatState { get; set; }
    public string? CurrentAdventureId { get; set; } // Link to generated adventure
}
