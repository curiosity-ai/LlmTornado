namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyNPC : FantasyEntity
{
    public string Background { get; set; } = string.Empty;
    public FantasyNPC(string name, string description, string background) : base(name, description)
    {
        Background = background;
        EntityType = FantasyEntityType.NPC;
    }
}
