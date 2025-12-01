using System.ComponentModel;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyLocation 
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool CanRestHere { get; set; }
    public FantasyRoute[] Routes { get; set; } = Array.Empty<FantasyRoute>();

    // Parameterless constructor for JSON deserialization
    public FantasyLocation()
    {
        Id = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        CanRestHere = false;
    }

    public FantasyLocation(string name, string description,string id, bool restLocation = false, FantasyRoute[] routes = null)
    {

        Id = id;
        Name = name;
        Description = description;
        CanRestHere = restLocation;
        Routes = routes ?? Array.Empty<FantasyRoute>();
    }

    public override string ToString()
    {
        return $@"{Name} : (Rest: {CanRestHere})

{Description} 

Routes: 
{string.Join(",\n", Routes.Select(r => r.ToString()))}
";
    }
}

public class FantasyRoute
{
    public string ToLocationId { get; set; }
    public string Description { get; set; } = string.Empty;

    public int DistanceInHours { get; set; }
    // Parameterless constructor for JSON deserialization
    public FantasyRoute()
    {
        ToLocationId = string.Empty;
        Description = string.Empty;
        DistanceInHours = 0;
    }
    public FantasyRoute(string toLocationId, string description, int distanceInHours)
    {
        ToLocationId = toLocationId;
        Description = description;
        DistanceInHours = distanceInHours;
    }
    public override string ToString()
    {
        return $"To Location: {ToLocationId}, Description: {Description}, Distance: {DistanceInHours} hours";
    }
}