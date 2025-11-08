namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

/// <summary>
/// Action types available in different phases
/// </summary>
public enum FantasyActionType
{
    // Exploring actions
    Move, //Move
    
    // Item actions
    LoseItem,
    GetItem,

    //Party Actions
    ActorJoinsParty,
    ActorLeavesParty,
}
