namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyWorldState
{
    public FantasyQuestLineProgress CurrentQuestLine { get; set; }
}

public class FantasyQuestLineProgress
{
    public FantasyQuestLine QuestLine { get; set; }
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