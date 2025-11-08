namespace LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;

public struct FantasyItemResult
{
    public string Name { get; set; }
    public string Description { get; set; }
}

public struct DetectedFantasyItems
{
    public List<FantasyItemResult> ItemsGained { get; set; }
    public List<FantasyItemResult> ItemsLost { get; set; }
}

