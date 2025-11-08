using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using System.ComponentModel;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

[Description("Game Action content")]
public struct FantasyActionContent
{
    [Description("The type of action being detected")]
    public FantasyActionType ActionType { get; set; }
    [Description("The content that was considered to contain the action to extract")]
    public string ActionContent { get; set; }
}
