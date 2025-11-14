namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Immutable compiled orchestration graph ready for execution.
/// </summary>
/// <typeparam name="TState">The type of state used by the orchestration.</typeparam>
internal class CompiledOrchestrationGraph<TState> : ICompiledOrchestrationGraph<TState> where TState : class, IOrchestrationState
{
    /// <inheritdoc/>
    public IOrchestrationGraph<TState> OriginalGraph { get; }

    /// <inheritdoc/>
    public GraphValidationResult Validation { get; }

    /// <inheritdoc/>
    public TState State { get; private set; }

    /// <inheritdoc/>
    public Type StateType => typeof(TState);

    /// <inheritdoc/>
    public OrchestrationExecutionOptions Options { get; }

    /// <inheritdoc/>
    public Dictionary<Type, List<GraphEdge>> EdgesBySourceType { get; }

    /// <inheritdoc/>
    public Dictionary<string, OrchestrationRunnableBase> RunnablesById { get; }

    /// <inheritdoc/>
    public ExecutionPlan ExecutionPlan { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledOrchestrationGraph{TState}"/> class.
    /// </summary>
    public CompiledOrchestrationGraph(
        IOrchestrationGraph<TState> originalGraph,
        TState initialState,
        GraphValidationResult validation,
        OrchestrationExecutionOptions options)
    {
        OriginalGraph = originalGraph ?? throw new ArgumentNullException(nameof(originalGraph));
        Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        State = initialState ?? throw new ArgumentNullException(nameof(initialState));
        Options = options ?? throw new ArgumentNullException(nameof(options));

        // Build optimized lookup structures
        EdgesBySourceType = BuildEdgesBySourceType(originalGraph);
        RunnablesById = BuildRunnablesById(originalGraph);
        ExecutionPlan = BuildExecutionPlan(originalGraph);
    }

    /// <summary>
    /// Updates the state instance (used during execution).
    /// </summary>
    internal void UpdateState(TState newState)
    {
        State = newState ?? throw new ArgumentNullException(nameof(newState));
    }

    private Dictionary<Type, List<GraphEdge>> BuildEdgesBySourceType(IOrchestrationGraph<TState> graph)
    {
        var result = new Dictionary<Type, List<GraphEdge>>();
        
        foreach (var edge in graph.Edges)
        {
            var sourceOutputType = edge.SourceRunnable.GetOutputType();
            if (!result.ContainsKey(sourceOutputType))
            {
                result[sourceOutputType] = new List<GraphEdge>();
            }
            result[sourceOutputType].Add(edge);
        }
        
        return result;
    }

    private Dictionary<string, OrchestrationRunnableBase> BuildRunnablesById(IOrchestrationGraph<TState> graph)
    {
        return graph.Runnables.ToDictionary(r => r.Id, r => r);
    }

    private ExecutionPlan BuildExecutionPlan(IOrchestrationGraph<TState> graph)
    {
        var plan = new ExecutionPlan();
        
        // Build metadata for each runnable
        foreach (var runnable in graph.Runnables)
        {
            var metadata = new RunnableExecutionMetadata(runnable);
            
            // Find outgoing edges
            metadata.OutgoingEdges.AddRange(graph.Edges.Where(e => e.SourceRunnable == runnable));
            
            // Find incoming edges
            metadata.IncomingEdges.AddRange(graph.Edges.Where(e => e.TargetRunnable == runnable));
            
            // Check if terminal
            metadata.IsTerminal = metadata.OutgoingEdges.Count == 0;
            
            if (metadata.IsTerminal)
            {
                plan.TerminalRunnables.Add(runnable);
            }
            
            plan.RunnableMetadata[runnable.Id] = metadata;
        }
        
        return plan;
    }
}

