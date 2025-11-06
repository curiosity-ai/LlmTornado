using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyWorldState
{
    public FantasyQuest CurrentQuest { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public string CurrentLocationName { get; set; } = "Tavern";
    public FantasyPlayer Player { get; set; }
    public List<FantasyNPC> Party { get; set; } = new();
    public Dictionary<string, FantasyScene> Locations { get; set; } = new();
    public List<FantasyQuest> CompletedQuest { get; set; } = new();
    public List<string> GameHistory { get; set; } = new();
    public int TurnNumber { get; set; } = 0;
}

