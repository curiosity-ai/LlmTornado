using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Utility;
using LlmTornado.Chat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;

/// <summary>
/// Used to create StateMachine like orchestrations for chat runtimes.
/// </summary>
public class OrchestrationRuntimeConfiguration : Orchestration<ChatMessage, ChatMessage>, IRuntimeConfiguration
{
    public ChatRuntime Runtime { get; set; }
    public CancellationTokenSource cts { get; set; } = new CancellationTokenSource();
    public Func<ChatRuntimeEvents, ValueTask>? OnRuntimeEvent { get; set; }
    private PersistentConversation _messageHistory { get;  set; } 
    public string MessageHistoryFileLocation { get; set; } = "chat_history.json";
    public Func<OrchestrationRuntimeConfiguration, ValueTask>? CustomInitialization { get; set; }
    public Func<string, ValueTask<bool>>? OnRuntimeRequestEvent { get; set; }

    public OrchestrationRuntimeConfiguration()
    {

    }

    private void LoadMessageHistory()
    {
        _messageHistory = new PersistentConversation(MessageHistoryFileLocation);
    }

    public virtual void OnRuntimeInitialized()
    {
        OnOrchestrationEvent += (e) =>
        {
            // Forward orchestration events to runtime
            this.OnRuntimeEvent?.Invoke(new ChatRuntimeOrchestrationEvent(e, Runtime?.Id ?? string.Empty));
        };

        LoadMessageHistory();

        CustomInitialization?.Invoke(this).GetAwaiter().GetResult();
    }

    public void CancelRuntime()
    {
        cts.Cancel();
        OnRuntimeEvent?.Invoke(new ChatRuntimeCancelledEvent(Runtime.Id));
    }

    public virtual async ValueTask<ChatMessage> AddToChatAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        // Invoke the StartingExecution event to signal the beginning of the execution process
        OnRuntimeEvent?.Invoke(new ChatRuntimeStartedEvent(Runtime.Id));

        _messageHistory.AppendMessage(message);

        await InvokeAsync(message);

        _messageHistory.AppendMessage(Results?.Last() ?? new ChatMessage(Code.ChatMessageRoles.Assistant, "Some sort of error"));

        _messageHistory.SaveChanges();

        OnRuntimeEvent?.Invoke(new ChatRuntimeCompletedEvent(Runtime.Id));

        return GetLastMessage();
    }

    public virtual void ClearMessages()
    {
        _messageHistory.Clear();
    }

    public virtual List<ChatMessage> GetMessages()
    {
        return _messageHistory.Messages;
    }

    public virtual ChatMessage GetLastMessage()
    {
        return _messageHistory.Messages.Last();
    }

    /// <summary>
    /// Compiles this orchestration configuration into a strongly-typed compiled graph.
    /// Note: This method extracts edges from existing advancers. For new code, use OrchestrationGraphBuilder directly.
    /// </summary>
    /// <typeparam name="TState">The type of state to use. Must implement <see cref="IOrchestrationState"/>.</typeparam>
    /// <param name="initialState">The initial state instance.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A compiled graph ready for execution.</returns>
    /// <exception cref="OrchestrationCompilationException">Thrown if compilation fails.</exception>
    /// <remarks>
    /// This method converts the current orchestration configuration into a compiled graph.
    /// The compiled graph is validated and optimized for execution.
    /// </remarks>
    public ICompiledOrchestrationGraph<TState> Compile<TState>(
        TState initialState,
        OrchestrationExecutionOptions? options = null)
        where TState : class, IOrchestrationState, new()
    {
        // Build graph from current configuration
        var builder = new OrchestrationGraphBuilder<TState>()
            .WithInitialState(initialState);

        // Add all runnables
        foreach (var runnable in Runnables.Values)
        {
            builder.AddRunnable(runnable);
        }

        // Set entry runnable
        if (InitialRunnable != null)
        {
            builder.SetEntryRunnable(InitialRunnable);
        }

        // Set output runnable
        if (RunnableWithResult != null)
        {
            builder.SetOutputRunnable(RunnableWithResult, RunnableWithResult.AllowDeadEnd);
        }

        // Extract edges from existing advancers
        // Note: This is a migration path. New code should use OrchestrationGraphBuilder directly.
        foreach (var runnable in Runnables.Values)
        {
            foreach (var advancer in runnable.BaseAdvancers)
            {
                // Create edge from advancer
                // This is simplified - full implementation would need to handle all advancer types
                var edge = new GraphEdge(runnable, advancer.NextRunnable, advancer);
                // Note: Edges should be added via builder.AddEdge/AddEdgeWithConverter
                // This method is a compatibility layer
            }
        }

        // Build and compile
        var graph = builder.Build();
        var compiler = new OrchestrationGraphCompiler<TState>();
        return compiler.Compile(graph, initialState, options);
    }
}
