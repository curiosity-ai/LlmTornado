using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyWorldState
{
    public FantasyQuestLineProgress CurrentQuestLine { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public string CurrentLocationName { get; set; } = "Tavern";
    public FantasyPlayer Player { get; set; }
    public List<FantasyPlayer> Bots { get; set; } = new();
    public Dictionary<string, Location> Locations { get; set; } = new();
    public List<string> QuestLog { get; set; } = new();
    public List<string> GameHistory { get; set; } = new();
    public int TurnNumber { get; set; } = 0;
}

internal class FantasyQuestLineProgress
{
    public FantasyAdventure QuestLine { get; set; }
    public int CurrentQuestIndex { get; set; } = 0;
    public FantasyQuest GetCurrentQuest()
    {
        if (QuestLine == null || QuestLine.Quests.Count == 0 || CurrentQuestIndex >= QuestLine.Quests.Count)
        {
            return null;
        }
        return QuestLine.Quests[CurrentQuestIndex];
    }
    public void AdvanceToNextQuest()
    {
        if (QuestLine != null && CurrentQuestIndex < QuestLine.Quests.Count - 1)
        {
            CurrentQuestIndex++;
        }
    }
}