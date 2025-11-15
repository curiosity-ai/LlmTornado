namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyLocation 
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; } = string.Empty;

    public FantasyLocation(string name, string description,string id )
    {

        Id = id;
        Name = name;
        Description = description;
    }

    public override string ToString()
    {
        return $@"{Name} : {Description}";
    }
}
