namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class  FantasyEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public FantasyEntityType EntityType { get; set; }

    public FantasyEntity(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
