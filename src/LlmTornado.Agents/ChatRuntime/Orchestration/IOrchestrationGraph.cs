namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Represents an immutable graph definition for orchestration.
/// This is the result of building a graph using <see cref="OrchestrationGraphBuilder{TState}"/>.
/// </summary>
/// <typeparam name="TState">The type of state used by this orchestration graph. Must implement <see cref="IOrchestrationState"/>.</typeparam>
public interface IOrchestrationGraph<TState> where TState : class, IOrchestrationState
{
    /// <summary>
    /// Gets the collection of runnables (nodes) in the graph.
    /// </summary>
    IReadOnlyList<OrchestrationRunnableBase> Runnables { get; }

    /// <summary>
    /// Gets the collection of edges (transitions) in the graph.
    /// </summary>
    IReadOnlyList<GraphEdge> Edges { get; }

    /// <summary>
    /// Gets the entry runnable that starts the orchestration.
    /// </summary>
    OrchestrationRunnableBase? EntryRunnable { get; }

    /// <summary>
    /// Gets the collection of exit runnables that terminate the orchestration.
    /// </summary>
    IReadOnlyList<OrchestrationRunnableBase> ExitRunnables { get; }

    /// <summary>
    /// Gets the initial state instance for this graph.
    /// </summary>
    TState InitialState { get; }
}

/// <summary>
/// Represents an edge (transition) between two runnables in the orchestration graph.
/// </summary>
public class GraphEdge
{
    /// <summary>
    /// Gets the source runnable (where the edge starts).
    /// </summary>
    public OrchestrationRunnableBase SourceRunnable { get; }

    /// <summary>
    /// Gets the target runnable (where the edge ends).
    /// </summary>
    public OrchestrationRunnableBase TargetRunnable { get; }

    /// <summary>
    /// Gets the advancer that defines the transition condition and conversion.
    /// </summary>
    public OrchestrationAdvancer Advancer { get; }

    /// <summary>
    /// Gets a value indicating whether this edge uses a converter.
    /// </summary>
    public bool HasConverter => Advancer.ConverterMethod != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphEdge"/> class.
    /// </summary>
    public GraphEdge(OrchestrationRunnableBase sourceRunnable, OrchestrationRunnableBase targetRunnable, OrchestrationAdvancer advancer)
    {
        SourceRunnable = sourceRunnable ?? throw new ArgumentNullException(nameof(sourceRunnable));
        TargetRunnable = targetRunnable ?? throw new ArgumentNullException(nameof(targetRunnable));
        Advancer = advancer ?? throw new ArgumentNullException(nameof(advancer));
    }
}

