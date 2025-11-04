using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Agents.Dnd.Agents.Runnables;

/// <summary>
/// Exit runnable for game completion
/// </summary>
public class ImprovedExitRunnable : OrchestrationRunnable<PhaseResult, ChatMessage>
{
    public ImprovedExitRunnable(Orchestration orchestrator, string runnableName = "") 
        : base(orchestrator, runnableName)
    {
    }

    public override ValueTask<ChatMessage> Invoke(RunnableProcess<PhaseResult, ChatMessage> process)
    {
        this.Orchestrator?.HasCompletedSuccessfully();
        
        string message = process.Input.ShouldContinue
            ? "Game session continues..." 
            : "Thank you for playing! Your progress has been saved.";
            
        return ValueTask.FromResult(new ChatMessage(ChatMessageRoles.Assistant, message));
    }
}
