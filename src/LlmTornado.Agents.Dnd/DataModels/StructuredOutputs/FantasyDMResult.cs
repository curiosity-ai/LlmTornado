using LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;
using System.ComponentModel;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

public struct FantasyDMResult
{
    [Description("The next narration based off the context")]
    public string Narration { get; set; }

    [Description("The current location of the player")]
    public string CurrentLocation { get; set; }

    [Description("The next actions the player can take")]
    public PlayerActionOptions[] NextActions { get; set; }

    [Description("The current act of the adventure")]
    public string CurrentAct { get; set; }

    [Description("The current scene of the adventure")]
    public string CurrentScene { get; set; }

    [Description("The completion percentage of the Scene")]
    public int SceneCompletionPercentage { get; set; }
}

public struct PlayerActionOptions
{
    [Description("The action the player can take")]
    public string Action { get; set; }

    [Description("Minimum number threshold for success on 20 sided dice Range:[2 easiest - 19 Hardest]")]
    public int MinimumSuccessThreshold { get; set; }

    [Description("The outcome if the action is successful")]
    public string SuccessOutcome { get; set; }
    
    [Description("The outcome if the action fails")]
    public string FailureOutcome { get; set; }
}