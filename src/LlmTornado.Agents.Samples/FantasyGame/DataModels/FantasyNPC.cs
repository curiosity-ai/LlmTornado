namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyNPC 
{
    public string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Id { get; set; }

    // Parameterless constructor for JSON deserialization
    public FantasyNPC()
    {
        Id = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
    }

    public FantasyNPC(string id,string name, string description) 
    {
        Id = id;
        Name = name;
        Description = description;
    }
}
