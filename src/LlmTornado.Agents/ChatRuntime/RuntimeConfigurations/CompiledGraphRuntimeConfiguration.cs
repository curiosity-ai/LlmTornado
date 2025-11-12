using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Code;
using System.Reflection;

namespace LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;

/// <summary>
/// Runtime configuration wrapper for compiled orchestration graphs (Agents 2.0).
/// </summary>
/// <typeparam name="TState">The type of state used by the orchestration.</typeparam>
internal class CompiledGraphRuntimeConfiguration<TState> : OrchestrationRuntimeConfiguration
    where TState : class, IOrchestrationState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledGraphRuntimeConfiguration{TState}"/> class.
    /// </summary>
    public CompiledGraphRuntimeConfiguration(ICompiledOrchestrationGraph<TState> compiledGraph)
    {
        CompiledGraph = compiledGraph ?? throw new ArgumentNullException(nameof(compiledGraph));
        
        // Initialize the base orchestration with the compiled graph's structure
        var originalGraph = compiledGraph.OriginalGraph;
        
        // Set entry runnable
        if (originalGraph.EntryRunnable != null)
        {
            SetEntryRunnable(originalGraph.EntryRunnable);
        }
        
        // Set output runnable (use first exit runnable, or entry if no exits)
        var outputRunnable = originalGraph.ExitRunnables.Count > 0 
            ? originalGraph.ExitRunnables[0] 
            : originalGraph.EntryRunnable;
        
        if (outputRunnable != null)
        {
            SetRunnableWithResult(outputRunnable);
        }
        
        // Register all runnables in the base class
        foreach (var runnable in originalGraph.Runnables)
        {
            Runnables[runnable.Id] = runnable;
            runnable.Orchestrator = this; // Set orchestrator reference
        }
        
        // Wire up advancers from edges
        foreach (var edge in originalGraph.Edges)
        {
            // Add the advancer to the source runnable
            // Use reflection to access internal AddAdvancer method
            var addAdvancerMethod = typeof(OrchestrationRunnableBase).GetMethod(
                "AddAdvancer",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(OrchestrationAdvancer) },
                null);
            
            addAdvancerMethod?.Invoke(edge.SourceRunnable, new object[] { edge.Advancer });
        }
    }

    /// <summary>
    /// Gets the compiled graph instance (for internal use by GetState).
    /// </summary>
    internal ICompiledOrchestrationGraph<TState> CompiledGraph { get; }

    /// <inheritdoc/>
    protected internal override TRequestedState TryGetState<TRequestedState>()
    {
        // Strongly-typed access: if the requested state type matches our compiled graph's state type, return it
        if (CompiledGraph.State is TRequestedState typedState)
        {
            return typedState;
        }
        return null!; // Type mismatch or not a compiled graph
    }

    /// <inheritdoc/>
    public override async ValueTask<ChatMessage> AddToChatAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        // Invoke the StartingExecution event
        OnRuntimeEvent?.Invoke(new ChatRuntimeStartedEvent(Runtime.Id));

        // Use compiled graph for execution via base class
        await base.AddToChatAsync(message, cancellationToken);

        OnRuntimeEvent?.Invoke(new ChatRuntimeCompletedEvent(Runtime.Id));

        return GetLastMessage();
    }
}

