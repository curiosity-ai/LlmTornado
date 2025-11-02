using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.Agents.Runnables;

/// <summary>
/// Result of phase management indicating current game phase and whether to continue
/// </summary>
public struct PhaseResult
{
    public GamePhase CurrentPhase { get; set; }
    public bool ShouldContinue { get; set; }
}
