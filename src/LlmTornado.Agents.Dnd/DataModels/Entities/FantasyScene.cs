namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyScene : FantasyEntity
{
    //Connected Locations
    public List<FantasyScene> ConnectedLocations { get; set; }
    public FantasyScene(string name, string description, List<FantasyScene> connectedLocations) : base(name, description)
    {
        ConnectedLocations = connectedLocations;
        EntityType = FantasyEntityType.Scene;
    }
}
