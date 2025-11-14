namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Represents a compiled, validated, and optimized orchestration graph ready for execution.
/// This is the result of runtime-compilation using <see cref="OrchestrationGraphCompiler{TState}"/>.
/// </summary>
/// <typeparam name="TState">The type of state used by this orchestration graph. Must implement <see cref="IOrchestrationState"/>.</typeparam>
public interface ICompiledOrchestrationGraph<TState> where TState : class, IOrchestrationState
{
    /// <summary>
    /// Gets the original graph definition that was compiled.
    /// </summary>
    IOrchestrationGraph<TState> OriginalGraph { get; }

    /// <summary>
    /// Gets the validation result from the compilation process.
    /// </summary>
    GraphValidationResult Validation { get; }

    /// <summary>
    /// Gets the current state instance for this orchestration execution.
    /// </summary>
    TState State { get; }

    /// <summary>
    /// Gets the type of state used by this graph.
    /// </summary>
    Type StateType { get; }

    /// <summary>
    /// Gets the execution options configured for this graph.
    /// </summary>
    OrchestrationExecutionOptions Options { get; }

    /// <summary>
    /// Gets a dictionary of edges grouped by source runnable output type for optimized lookup during execution.
    /// </summary>
    Dictionary<Type, List<GraphEdge>> EdgesBySourceType { get; }

    /// <summary>
    /// Gets a dictionary of runnables indexed by their ID for fast lookup during execution.
    /// </summary>
    Dictionary<string, OrchestrationRunnableBase> RunnablesById { get; }

    /// <summary>
    /// Gets the execution plan containing optimized structures for graph execution.
    /// </summary>
    ExecutionPlan ExecutionPlan { get; }
}

/// <summary>
/// Represents an optimized execution plan for running the orchestration graph.
/// </summary>
public class ExecutionPlan
{
    /// <summary>
    /// Gets a dictionary mapping runnable IDs to their optimized execution metadata.
    /// </summary>
    public Dictionary<string, RunnableExecutionMetadata> RunnableMetadata { get; } = new();

    /// <summary>
    /// Gets a list of all terminal runnables (nodes with no outgoing edges).
    /// </summary>
    public List<OrchestrationRunnableBase> TerminalRunnables { get; } = new();
}

/// <summary>
/// Contains metadata about a runnable for optimized execution.
/// </summary>
public class RunnableExecutionMetadata
{
    /// <summary>
    /// Gets the runnable this metadata describes.
    /// </summary>
    public OrchestrationRunnableBase Runnable { get; }

    /// <summary>
    /// Gets a list of edges that originate from this runnable.
    /// </summary>
    public List<GraphEdge> OutgoingEdges { get; } = new();

    /// <summary>
    /// Gets a list of edges that terminate at this runnable.
    /// </summary>
    public List<GraphEdge> IncomingEdges { get; } = new();

    /// <summary>
    /// Gets a value indicating whether this runnable is a terminal node (no outgoing edges).
    /// </summary>
    public bool IsTerminal { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RunnableExecutionMetadata"/> class.
    /// </summary>
    public RunnableExecutionMetadata(OrchestrationRunnableBase runnable)
    {
        Runnable = runnable ?? throw new ArgumentNullException(nameof(runnable));
    }
}

