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

public enum FantasyEntityType
{
    Creature,
    Item,
    Location
}

public class FantasyLocation : FantasyEntity
{
    public List<string> ConnectedLocations { get; set; }
    public FantasyLocation(string name, string description, List<string> connectedLocations) : base(name, description)
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


public class FantasyCreature : FantasyEntity
{
    public int HitPoints { get; set; } = 4;
    public FantasyCreature(string name, string description, int hitPoints) : base(name, description)
    {
        HitPoints = hitPoints;
        EntityType = FantasyEntityType.Creature;
    }
}
