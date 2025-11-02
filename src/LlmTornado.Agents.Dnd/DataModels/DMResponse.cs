namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents the DM's response to a player action
/// </summary>
public struct DMResponse
{
    public string Narrative { get; set; }
    public string ActionResult { get; set; }
    public List<string> NewQuestItems { get; set; }
    public Dictionary<string, int> StatChanges { get; set; }
    public bool CombatInitiated { get; set; }

    public DMResponse()
    {
        Narrative = string.Empty;
        ActionResult = string.Empty;
        NewQuestItems = new List<string>();
        StatChanges = new Dictionary<string, int>();
        CombatInitiated = false;
    }
}
