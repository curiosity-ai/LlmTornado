namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Game phases
/// </summary>
public enum GamePhase
{
    Adventuring,
    Combat,
    Shopping,
    Resting
}

/// <summary>
/// Action types available in different phases
/// </summary>
public enum ActionType
{
    // Adventuring actions
    Move,
    Travel,
    Talk,
    Examine,
    Search,
    Rest,
    EnterShop,
    
    // Combat actions
    Attack,
    UseItem,
    CombatMove,
    Defend,
    Retreat,
    
    // Shop actions
    Buy,
    Sell,
    ExitShop,
    
    // General actions
    ViewInventory,
    ViewStatus,
    Quit
}

/// <summary>
/// Represents an action with its context
/// </summary>
public class GameAction
{
    public ActionType Type { get; set; }
    public string? Target { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string PlayerName { get; set; } = string.Empty;

    public override string ToString()
    {
        var paramStr = Parameters.Any() 
            ? $"\nParameters: {string.Join(", ", Parameters.Select(kv => $"{kv.Key}={kv.Value}"))}" 
            : "";
        return $"{PlayerName}: {Type} -> {Target ?? "none"}\n{Description}{paramStr}";
    }
}
