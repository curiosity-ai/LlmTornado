namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Fluent builder for creating orchestration graphs with strongly-typed state.
/// </summary>
/// <typeparam name="TState">The type of state used by this orchestration. Must implement <see cref="IOrchestrationState"/> and have a parameterless constructor.</typeparam>
public class OrchestrationGraphBuilder<TState> where TState : class, IOrchestrationState, new()
{
    private readonly List<OrchestrationRunnableBase> _runnables = new();
    private readonly List<GraphEdge> _edges = new();
    private OrchestrationRunnableBase? _entryRunnable;
    private readonly List<OrchestrationRunnableBase> _exitRunnables = new();
    private TState _initialState = new TState();
    private bool _isBuilt = false;

    /// <summary>
    /// Sets the initial state for the orchestration graph.
    /// </summary>
    /// <param name="state">The initial state instance.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public OrchestrationGraphBuilder<TState> WithInitialState(TState state)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Cannot modify graph after Build() has been called.");
        
        _initialState = state ?? throw new ArgumentNullException(nameof(state));
        return this;
    }

    /// <summary>
    /// Adds a runnable (node) to the graph.
    /// </summary>
    /// <param name="runnable">The runnable to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public OrchestrationGraphBuilder<TState> AddRunnable(OrchestrationRunnableBase runnable)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Cannot modify graph after Build() has been called.");
        
        if (runnable == null)
            throw new ArgumentNullException(nameof(runnable));
        
        if (!_runnables.Contains(runnable))
        {
            _runnables.Add(runnable);
        }
        
        return this;
    }

    /// <summary>
    /// Sets the entry runnable that starts the orchestration.
    /// </summary>
    /// <param name="entryRunnable">The entry runnable.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public OrchestrationGraphBuilder<TState> SetEntryRunnable(OrchestrationRunnableBase entryRunnable)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Cannot modify graph after Build() has been called.");
        
        if (entryRunnable == null)
            throw new ArgumentNullException(nameof(entryRunnable));
        
        AddRunnable(entryRunnable);
        _entryRunnable = entryRunnable;
        return this;
    }

    /// <summary>
    /// Sets the output runnable that produces the final result.
    /// </summary>
    /// <param name="outputRunnable">The output runnable.</param>
    /// <param name="withDeadEnd">Whether this runnable should allow dead end (no outgoing edges).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public OrchestrationGraphBuilder<TState> SetOutputRunnable(OrchestrationRunnableBase outputRunnable, bool withDeadEnd = false)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Cannot modify graph after Build() has been called.");
        
        if (outputRunnable == null)
            throw new ArgumentNullException(nameof(outputRunnable));
        
        AddRunnable(outputRunnable);
        
        if (withDeadEnd)
        {
            outputRunnable.AllowDeadEnd = true;
        }
        
        if (!_exitRunnables.Contains(outputRunnable))
        {
            _exitRunnables.Add(outputRunnable);
        }
        
        return this;
    }

    /// <summary>
    /// Adds an edge between two runnables where the output type matches the input type.
    /// </summary>
    /// <typeparam name="TOutput">The output type of the source runnable and input type of the target runnable.</typeparam>
    /// <param name="source">The source runnable.</param>
    /// <param name="target">The target runnable.</param>
    /// <param name="condition">Optional condition that must be met for the transition to occur.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public OrchestrationGraphBuilder<TState> AddEdge<TOutput>(
        OrchestrationRunnableBase source,
        OrchestrationRunnableBase target,
        AdvancementRequirement<TOutput>? condition = null)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Cannot modify graph after Build() has been called.");
        
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        
        AddRunnable(source);
        AddRunnable(target);
        
        var advancer = new OrchestrationAdvancer<TOutput>(condition ?? (_ => true), target);
        var edge = new GraphEdge(source, target, advancer);
        _edges.Add(edge);
        
        return this;
    }

    /// <summary>
    /// Adds an edge between two runnables with a type converter.
    /// Use this when the source output type doesn't match the target input type.
    /// </summary>
    /// <typeparam name="TSource">The output type of the source runnable.</typeparam>
    /// <typeparam name="TTarget">The input type of the target runnable.</typeparam>
    /// <param name="source">The source runnable.</param>
    /// <param name="target">The target runnable.</param>
    /// <param name="converter">The converter function that transforms source output to target input.</param>
    /// <param name="condition">Optional condition that must be met for the transition to occur.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public OrchestrationGraphBuilder<TState> AddEdgeWithConverter<TSource, TTarget>(
        OrchestrationRunnableBase source,
        OrchestrationRunnableBase target,
        AdvancementResultConverter<TSource, TTarget> converter,
        AdvancementRequirement<TSource>? condition = null)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Cannot modify graph after Build() has been called.");
        
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (converter == null)
            throw new ArgumentNullException(nameof(converter));
        
        AddRunnable(source);
        AddRunnable(target);
        
        var advancer = new OrchestrationAdvancer<TSource, TTarget>(condition ?? (_ => true), converter, target);
        var edge = new GraphEdge(source, target, advancer);
        _edges.Add(edge);
        
        return this;
    }

    /// <summary>
    /// Builds the immutable graph definition.
    /// </summary>
    /// <returns>An immutable graph definition ready for compilation.</returns>
    public IOrchestrationGraph<TState> Build()
    {
        if (_isBuilt)
            throw new InvalidOperationException("Build() has already been called on this builder.");
        
        if (_entryRunnable == null)
            throw new InvalidOperationException("Entry runnable must be set before building the graph.");
        
        _isBuilt = true;
        return new OrchestrationGraph<TState>(
            _runnables.ToList(),
            _edges.ToList(),
            _entryRunnable,
            _exitRunnables.ToList(),
            _initialState
        );
    }
}

