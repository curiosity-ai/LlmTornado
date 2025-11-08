using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyWorldState
{
    public string AdventureTitle { get; set; } = "";
    public string AdventureFile { get; set; } = "";
    public string MemoryFile { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public string CurrentLocationName { get; set; } = "Unknown";
    public FantasyPlayer Player { get; set; }
}

