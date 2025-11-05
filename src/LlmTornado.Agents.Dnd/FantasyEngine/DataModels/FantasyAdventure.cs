namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyAdventure
{
    public List<FantasyQuest> Quests { get; set; } = new List<FantasyQuest>();
    public List<FantasyLocation> Locations { get; set; } = new List<FantasyLocation>();
    public List<FantasyItem> Items { get; set; } = new List<FantasyItem>();
}

