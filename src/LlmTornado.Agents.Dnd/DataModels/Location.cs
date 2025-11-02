namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents a location in the game world
/// </summary>
public class Location
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Exits { get; set; } = new();
    public List<string> NPCs { get; set; } = new();
    public List<Item> Items { get; set; } = new();
    public bool IsVisited { get; set; } = false;
}
