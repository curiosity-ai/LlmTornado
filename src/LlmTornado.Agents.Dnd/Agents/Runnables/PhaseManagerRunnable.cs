using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat.Models;

namespace LlmTornado.Agents.Dnd.Agents.Runnables;

/// <summary>
/// Manages phase transitions and game state
/// </summary>
public class PhaseManagerRunnable : OrchestrationRunnable<ChatMessage, PhaseResult>
{
    private readonly GameState _gameState;
    private readonly CombatManager _combatManager;

    public PhaseManagerRunnable(Orchestration orchestrator, GameState gameState, CombatManager combatManager) 
        : base(orchestrator)
    {
        _gameState = gameState;
        _combatManager = combatManager;
    }

    public override ValueTask<PhaseResult> Invoke(RunnableProcess<ChatMessage, PhaseResult> process)
    {
        // Handle combat ending
        if (ShouldEndCombat())
        {
            EndCombat();
        }

        return ValueTask.FromResult(new PhaseResult
        {
            CurrentPhase = _gameState.CurrentPhase,
            ShouldContinue = true
        });
    }
    
    /// <summary>
    /// Checks if combat is active but has ended
    /// </summary>
    private bool ShouldEndCombat()
    {
        return _gameState.CurrentPhase == GamePhase.Combat && 
               _gameState.CombatState != null && 
               _gameState.CombatState.IsCombatOver();
    }
    
    /// <summary>
    /// Ends combat and displays end message
    /// </summary>
    private void EndCombat()
    {
        string endMessage = _combatManager.EndCombat();
        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine(endMessage);
        Console.WriteLine(new string('═', 80) + "\n");
    }
}
