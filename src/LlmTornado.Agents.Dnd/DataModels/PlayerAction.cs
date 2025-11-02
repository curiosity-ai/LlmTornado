namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents a player's action in the game
/// </summary>
public struct PlayerAction
{
    public string ActionType { get; set; } // explore, attack, talk, use_item, rest, etc.
    public string Target { get; set; }
    public string Description { get; set; }
    public Dictionary<string, string> Parameters { get; set; }

    public PlayerAction(string actionType, string target, string description, Dictionary<string, string>? parameters = null)
    {
        ActionType = actionType;
        Target = target;
        Description = description;
        Parameters = parameters ?? new Dictionary<string, string>();
    }

    public override string ToString()
    {
        return @$"
Action: {ActionType} -> Target: {Target}

Description:
{Description}

Parameters?: 
{string.Join(", ", Parameters.Select(kv => kv.Key + "=" + kv.Value))}

";
    }
}
