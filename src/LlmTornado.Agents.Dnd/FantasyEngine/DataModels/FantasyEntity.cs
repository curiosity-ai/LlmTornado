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

public class FantasyLocation : FantasyEntity
{
    public List<FantasyLocation> ConnectedLocations { get; set; }
    public FantasyLocation(string name, string description, List<FantasyLocation> connectedLocations) : base(name, description)
    {
        ConnectedLocations = connectedLocations;
        EntityType = FantasyEntityType.Location;
    }
}

public class FantasyItem : FantasyEntity
{
    public FantasyItem(string name, string description) : base(name, description)
    {
        EntityType = FantasyEntityType.Item;
    }
}


public class FantasyNPC : FantasyEntity
{
    public string Background { get; set; } = string.Empty;
    public FantasyNPC(string name, string description, string background) : base(name, description)
    {
        Background = background;
        EntityType = FantasyEntityType.NPC;
    }
}
