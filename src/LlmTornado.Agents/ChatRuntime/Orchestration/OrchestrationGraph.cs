namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Immutable implementation of an orchestration graph definition.
/// </summary>
/// <typeparam name="TState">The type of state used by this orchestration graph.</typeparam>
internal class OrchestrationGraph<TState> : IOrchestrationGraph<TState> where TState : class, IOrchestrationState
{
    /// <inheritdoc/>
    public IReadOnlyList<OrchestrationRunnableBase> Runnables { get; }

    /// <inheritdoc/>
    public IReadOnlyList<GraphEdge> Edges { get; }

    /// <inheritdoc/>
    public OrchestrationRunnableBase? EntryRunnable { get; }

    /// <inheritdoc/>
    public IReadOnlyList<OrchestrationRunnableBase> ExitRunnables { get; }

    /// <inheritdoc/>
    public TState InitialState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationGraph{TState}"/> class.
    /// </summary>
    public OrchestrationGraph(
        List<OrchestrationRunnableBase> runnables,
        List<GraphEdge> edges,
        OrchestrationRunnableBase? entryRunnable,
        List<OrchestrationRunnableBase> exitRunnables,
        TState initialState)
    {
        Runnables = runnables?.AsReadOnly() ?? throw new ArgumentNullException(nameof(runnables));
        Edges = edges?.AsReadOnly() ?? throw new ArgumentNullException(nameof(edges));
        EntryRunnable = entryRunnable;
        ExitRunnables = exitRunnables?.AsReadOnly() ?? throw new ArgumentNullException(nameof(exitRunnables));
        InitialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
    }
}

