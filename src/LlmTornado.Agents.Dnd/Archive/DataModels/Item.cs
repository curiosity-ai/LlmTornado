namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents an item in the game
/// </summary>
public class Item
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // weapon, armor, consumable, quest
    public int Value { get; set; } = 0;
    public Dictionary<string, int> Properties { get; set; } = new();
}
