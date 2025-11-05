using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyQuest
{
    // Title of the quest
    public string Title { get; set; }

    //What the quest is about
    public string Description { get; set; }

    //Objective to complete the quest
    public string Objective { get; set; }
}
public class FantasyQuestProgress
{
    // The quest being tracked
    public FantasyQuest Quest { get; set; }
    // Whether the quest is completed
    public bool IsCompleted { get; set; } = false;
    // Details about the progress made
    public string ProgressDetails { get; set; }
    // Details about the results of the quest
    public string CompletionDetails { get; set; }

}

public class FantasyQuestLine
{
    public List<FantasyQuest> Quests { get; set; } = new List<FantasyQuest>();
    public List<FantasyLocation> Locations { get; set; } = new List<FantasyLocation>();
    public List<FantasyItem> Items { get; set; } = new List<FantasyItem>();
}

