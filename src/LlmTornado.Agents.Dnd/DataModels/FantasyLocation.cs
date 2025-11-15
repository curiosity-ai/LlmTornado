namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyLocation 
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool CanRestHere { get; set; }

    // Parameterless constructor for JSON deserialization
    public FantasyLocation()
    {
        Id = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        CanRestHere = false;
    }

    public FantasyLocation(string name, string description,string id, bool restLocation = false)
    {

        Id = id;
        Name = name;
        Description = description;
        CanRestHere = restLocation;
    }

    public override string ToString()
    {
        return $@"{Name} : (Rest: {CanRestHere})

{Description} ";
    }
}
