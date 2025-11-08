namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyItem : FantasyEntity
{
    public FantasyItem(string name, string description) : base(name, description)
    {
        EntityType = FantasyEntityType.Item;
    }
}
