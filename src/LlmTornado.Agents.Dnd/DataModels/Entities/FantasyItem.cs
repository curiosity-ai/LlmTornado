namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyItem 
{
    public string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Id { get; set; }
    public FantasyItem(string id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }
}
