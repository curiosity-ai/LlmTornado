namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents the DM's response to a player action
/// </summary>
public struct DMResponse
{
    public string Narrative { get; set; }
    public string? ActionResult { get; set; } = string.Empty;
    public string[]? NewQuestItems { get; set; } = Array.Empty<string>();
    public StatChange[]? StatChanges { get; set; } = Array.Empty<StatChange>();
    public bool CombatInitiated { get; set; } = false;

    public DMResponse()
    {
        Narrative = string.Empty;
        ActionResult = string.Empty;
        NewQuestItems = Array.Empty<string>();
        StatChanges = Array.Empty<StatChange>();
        CombatInitiated = false;
    }
}

public struct StatChange
{
    public string StatName { get; set; }
    public int ChangeAmount { get; set; }
    public StatChange(string statName, int changeAmount)
    {
        StatName = statName;
        ChangeAmount = changeAmount;
    }
}
