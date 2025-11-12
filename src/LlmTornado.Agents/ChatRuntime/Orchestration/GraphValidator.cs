using System.Reflection;

namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Validates orchestration graphs during runtime-compilation.
/// </summary>
/// <typeparam name="TState">The type of state used by the orchestration.</typeparam>
public class GraphValidator<TState> where TState : class, IOrchestrationState
{
    private readonly GraphValidationResult _result = new();

    /// <summary>
    /// Gets the validation result.
    /// </summary>
    public GraphValidationResult Result => _result;

    /// <summary>
    /// Validates the entire graph structure.
    /// </summary>
    public void ValidateAll(IOrchestrationGraph<TState> graph)
    {
        ValidateTypeCompatibility(graph);
        ValidateReachability(graph);
        ValidateTerminalNodes(graph);
        ValidateStateCompleteness(graph);
        ValidateNoCycles(graph);
        ValidateConditions(graph);
        ValidateConverters(graph);
    }

    /// <summary>
    /// Validates type compatibility between runnables and their edges.
    /// </summary>
    public void ValidateTypeCompatibility(IOrchestrationGraph<TState> graph)
    {
        foreach (var edge in graph.Edges)
        {
            var sourceType = GetRunnableOutputType(edge.SourceRunnable);
            var targetType = GetRunnableInputType(edge.TargetRunnable);

            if (edge.HasConverter)
            {
                // Validate converter signature
                var converterType = edge.Advancer.ConverterMethod?.GetType();
                if (converterType != null && converterType.IsGenericType)
                {
                    var genericArgs = converterType.GetGenericArguments();
                    if (genericArgs.Length >= 2)
                    {
                        var converterInputType = genericArgs[0];
                        var converterOutputType = genericArgs[1];

                        if (!sourceType.IsAssignableFrom(converterInputType))
                        {
                            _result.AddError(
                                $"Converter input type {converterInputType.Name} doesn't match source output type {sourceType.Name}",
                                $"Ensure the converter's input type matches {sourceType.Name}",
                                edge.SourceRunnable,
                                edge
                            );
                        }

                        if (!targetType.IsAssignableFrom(converterOutputType))
                        {
                            _result.AddError(
                                $"Converter output type {converterOutputType.Name} doesn't match target input type {targetType.Name}",
                                $"Ensure the converter's output type matches {targetType.Name}",
                                edge.TargetRunnable,
                                edge
                            );
                        }
                    }
                }
            }
            else
            {
                // Direct edge - types must match
                if (!targetType.IsAssignableFrom(sourceType) && !sourceType.IsAssignableFrom(targetType))
                {
                    _result.AddError(
                        $"Type mismatch: source output type {sourceType.Name} doesn't match target input type {targetType.Name}",
                        $"Use AddEdgeWithConverter to convert from {sourceType.Name} to {targetType.Name}",
                        edge.SourceRunnable,
                        edge
                    );
                }
            }
        }
    }

    /// <summary>
    /// Validates that all runnables are reachable from the entry point.
    /// </summary>
    public void ValidateReachability(IOrchestrationGraph<TState> graph)
    {
        if (graph.EntryRunnable == null)
        {
            _result.AddError("Entry runnable is not set", "Call SetEntryRunnable() to set the entry point");
            return;
        }

        var reachable = new HashSet<OrchestrationRunnableBase>();
        var queue = new Queue<OrchestrationRunnableBase>();
        queue.Enqueue(graph.EntryRunnable);
        reachable.Add(graph.EntryRunnable);

        // BFS from entry runnable
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var outgoingEdges = graph.Edges.Where(e => e.SourceRunnable == current);
            
            foreach (var edge in outgoingEdges)
            {
                if (!reachable.Contains(edge.TargetRunnable))
                {
                    reachable.Add(edge.TargetRunnable);
                    queue.Enqueue(edge.TargetRunnable);
                }
            }
        }

        // Check for unreachable runnables
        foreach (var runnable in graph.Runnables)
        {
            if (!reachable.Contains(runnable))
            {
                _result.AddError(
                    $"Runnable '{runnable.RunnableName}' is unreachable from entry point",
                    "Add an edge from entry runnable or remove the unreachable runnable",
                    runnable
                );
            }
        }

        // Validate exit runnables are reachable
        foreach (var exitRunnable in graph.ExitRunnables)
        {
            if (!reachable.Contains(exitRunnable))
            {
                _result.AddWarning(
                    $"Exit runnable '{exitRunnable.RunnableName}' is unreachable",
                    "Ensure there's a path from entry to this exit runnable",
                    exitRunnable
                );
            }
        }
    }

    /// <summary>
    /// Validates terminal nodes and auto-detects them.
    /// </summary>
    public void ValidateTerminalNodes(IOrchestrationGraph<TState> graph)
    {
        foreach (var runnable in graph.Runnables)
        {
            var outgoingEdges = graph.Edges.Where(e => e.SourceRunnable == runnable).ToList();
            
            if (outgoingEdges.Count == 0)
            {
                // Terminal node detected
                if (!runnable.AllowDeadEnd)
                {
                    _result.AddError(
                        $"Terminal node '{runnable.RunnableName}' has no outgoing edges but AllowDeadEnd is not set",
                        "Set AllowDeadEnd = true on this runnable or add an outgoing edge",
                        runnable
                    );
                }
            }
            else
            {
                // Non-terminal node
                if (runnable.AllowDeadEnd)
                {
                    _result.AddWarning(
                        $"Runnable '{runnable.RunnableName}' has outgoing edges but AllowDeadEnd is set to true",
                        "Consider removing AllowDeadEnd if this runnable should advance",
                        runnable
                    );
                }
            }
        }
    }

    /// <summary>
    /// Validates state completeness by checking that all required properties exist.
    /// </summary>
    public void ValidateStateCompleteness(IOrchestrationGraph<TState> graph)
    {
        var stateType = typeof(TState);
        var stateProperties = stateType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var runnable in graph.Runnables)
        {
            var requiredProperties = GetRequiredStateProperties(runnable);
            
            foreach (var propertyName in requiredProperties)
            {
                if (!stateProperties.ContainsKey(propertyName))
                {
                    _result.AddError(
                        $"Runnable '{runnable.RunnableName}' requires state property '{propertyName}' but it doesn't exist in {stateType.Name}",
                        $"Add property '{propertyName}' to {stateType.Name} or remove [RequiresStateProperty(\"{propertyName}\")] attribute",
                        runnable
                    );
                }
            }
        }
    }

    /// <summary>
    /// Validates that there are no cycles in the graph (unless explicitly allowed).
    /// </summary>
    public void ValidateNoCycles(IOrchestrationGraph<TState> graph)
    {
        var visited = new HashSet<OrchestrationRunnableBase>();
        var recursionStack = new HashSet<OrchestrationRunnableBase>();

        foreach (var runnable in graph.Runnables)
        {
            if (!visited.Contains(runnable))
            {
                if (HasCycle(runnable, graph, visited, recursionStack, new List<OrchestrationRunnableBase>()))
                {
                    // Cycle detected - this is a warning, not an error, as cycles can be intentional
                    _result.AddWarning(
                        $"Potential cycle detected involving runnable '{runnable.RunnableName}'",
                        "Ensure cycles have proper exit conditions to prevent infinite loops",
                        runnable
                    );
                }
            }
        }
    }

    /// <summary>
    /// Validates that condition delegates have correct signatures.
    /// </summary>
    public void ValidateConditions(IOrchestrationGraph<TState> graph)
    {
        foreach (var edge in graph.Edges)
        {
            var conditionMethod = edge.Advancer.InvokeMethod;
            if (conditionMethod != null)
            {
                var sourceOutputType = GetRunnableOutputType(edge.SourceRunnable);
                
                // Check if condition is a delegate
                if (conditionMethod is Delegate conditionDelegate)
                {
                    var methodInfo = conditionDelegate.Method;
                    var parameters = methodInfo.GetParameters();
                    
                    if (parameters.Length > 0)
                    {
                        var parameterType = parameters[0].ParameterType;
                        if (!parameterType.IsAssignableFrom(sourceOutputType) && !sourceOutputType.IsAssignableFrom(parameterType))
                        {
                            _result.AddError(
                                $"Condition parameter type {parameterType.Name} doesn't match source output type {sourceOutputType.Name}",
                                $"Update condition signature to accept {sourceOutputType.Name}",
                                edge.SourceRunnable,
                                edge
                            );
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates that converter delegates have correct signatures.
    /// </summary>
    public void ValidateConverters(IOrchestrationGraph<TState> graph)
    {
        foreach (var edge in graph.Edges.Where(e => e.HasConverter))
        {
            var converterMethod = edge.Advancer.ConverterMethod;
            if (converterMethod != null)
            {
                var sourceOutputType = GetRunnableOutputType(edge.SourceRunnable);
                var targetInputType = GetRunnableInputType(edge.TargetRunnable);
                
                if (converterMethod is Delegate converterDelegate)
                {
                    var methodInfo = converterDelegate.Method;
                    var parameters = methodInfo.GetParameters();
                    var returnType = methodInfo.ReturnType;
                    
                    if (parameters.Length > 0)
                    {
                        var parameterType = parameters[0].ParameterType;
                        if (!parameterType.IsAssignableFrom(sourceOutputType))
                        {
                            _result.AddError(
                                $"Converter input type {parameterType.Name} doesn't match source output type {sourceOutputType.Name}",
                                $"Update converter to accept {sourceOutputType.Name}",
                                edge.SourceRunnable,
                                edge
                            );
                        }
                    }
                    
                    if (!targetInputType.IsAssignableFrom(returnType))
                    {
                        _result.AddError(
                            $"Converter return type {returnType.Name} doesn't match target input type {targetInputType.Name}",
                            $"Update converter to return {targetInputType.Name}",
                            edge.TargetRunnable,
                            edge
                        );
                    }
                }
            }
        }
    }

    private Type GetRunnableInputType(OrchestrationRunnableBase runnable)
    {
        return runnable.GetInputType();
    }

    private Type GetRunnableOutputType(OrchestrationRunnableBase runnable)
    {
        return runnable.GetOutputType();
    }

    private List<string> GetRequiredStateProperties(OrchestrationRunnableBase runnable)
    {
        var properties = new List<string>();
        var runnableType = runnable.GetType();
        
        // Find Invoke method
        var invokeMethod = runnableType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (invokeMethod == null)
        {
            // Try inherited method
            invokeMethod = runnableType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
        }
        
        if (invokeMethod != null)
        {
            var attributes = invokeMethod.GetCustomAttributes<RequiresStatePropertyAttribute>();
            properties.AddRange(attributes.Select(a => a.PropertyName));
        }
        
        return properties;
    }

    private bool HasCycle(
        OrchestrationRunnableBase runnable,
        IOrchestrationGraph<TState> graph,
        HashSet<OrchestrationRunnableBase> visited,
        HashSet<OrchestrationRunnableBase> recursionStack,
        List<OrchestrationRunnableBase> path)
    {
        visited.Add(runnable);
        recursionStack.Add(runnable);
        path.Add(runnable);

        var outgoingEdges = graph.Edges.Where(e => e.SourceRunnable == runnable);
        foreach (var edge in outgoingEdges)
        {
            if (!visited.Contains(edge.TargetRunnable))
            {
                if (HasCycle(edge.TargetRunnable, graph, visited, recursionStack, path))
                {
                    return true;
                }
            }
            else if (recursionStack.Contains(edge.TargetRunnable))
            {
                // Cycle found
                return true;
            }
        }

        recursionStack.Remove(runnable);
        path.RemoveAt(path.Count - 1);
        return false;
    }
}

