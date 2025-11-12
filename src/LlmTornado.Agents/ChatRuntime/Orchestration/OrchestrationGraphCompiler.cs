namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Compiles orchestration graphs by validating and optimizing them for execution.
/// </summary>
/// <typeparam name="TState">The type of state used by the orchestration.</typeparam>
public class OrchestrationGraphCompiler<TState> where TState : class, IOrchestrationState
{
    /// <summary>
    /// Compiles a graph definition into an optimized, validated graph ready for execution.
    /// </summary>
    /// <param name="graph">The graph definition to compile.</param>
    /// <param name="initialState">The initial state instance for execution.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A compiled graph ready for execution.</returns>
    /// <exception cref="OrchestrationCompilationException">Thrown if the graph fails validation.</exception>
    public ICompiledOrchestrationGraph<TState> Compile(
        IOrchestrationGraph<TState> graph,
        TState initialState,
        OrchestrationExecutionOptions? options = null)
    {
        if (graph == null)
            throw new ArgumentNullException(nameof(graph));
        if (initialState == null)
            throw new ArgumentNullException(nameof(initialState));

        options ??= new OrchestrationExecutionOptions();

        // Validate the graph
        var validator = new GraphValidator<TState>();
        validator.ValidateAll(graph);

        if (!validator.Result.IsValid)
        {
            throw new OrchestrationCompilationException(validator.Result);
        }

        // Build optimized structures
        var compiledGraph = new CompiledOrchestrationGraph<TState>(
            graph,
            initialState,
            validator.Result,
            options
        );

        return compiledGraph;
    }
}

