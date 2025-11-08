using LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;
using System.ComponentModel;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

public struct FantasyDMResult
{

    [Description("The next narration based off the context")]
    public string Narration { get; set; }


    //[Description("Game Actions to process")]
    //public FantasyActionContent[] Actions { get; set; }

    public string CurrentLocation { get; set; }
}
