namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyAdventure
{
    public string AdventureMdPath { get; set; }
    //First Quest to start the Adventure
    public FantasyQuest InitialQuest { get; set; }

    //Predefined Quests in the Adventure
    public FantasyQuest[] Quests { get; set; }

    //Predefined Locations in the World
    public FantasyScene[] Locations { get; set; } 

    //Possible World Items
    public FantasyItem[] Items { get; set; }
}

