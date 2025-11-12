using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Samples.Compatibility;

/// <summary>
/// Compatibility wrapper for OrchestrationBuilder (samples/examples only - not shipped publicly).
/// This provides backward compatibility for existing sample code while the library migrates to Agents 2.0.
/// </summary>
public class OrchestrationBuilder
{
    private readonly OrchestrationRuntimeConfiguration _config;

    public OrchestrationBuilder(OrchestrationRuntimeConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public OrchestrationBuilder SetEntryRunnable(OrchestrationRunnableBase entryRunnable)
    {
        _config.SetEntryRunnable(entryRunnable);
        return this;
    }

    public OrchestrationBuilder SetOutputRunnable(OrchestrationRunnableBase outputRunnable)
    {
        _config.SetRunnableWithResult(outputRunnable);
        return this;
    }

    public OrchestrationBuilder WithRuntimeProperty(string key, object value)
    {
        _config.RuntimeProperties.AddOrUpdate(key, value, (k, v) => value);
        return this;
    }

    public OrchestrationBuilder WithRuntimeInitializer(Func<OrchestrationRuntimeConfiguration, ValueTask> initializer)
    {
        _config.CustomInitialization = initializer;
        return this;
    }

    public OrchestrationBuilder WithChatMemory(string conversationFile)
    {
        _config.MessageHistoryFileLocation = conversationFile;
        return this;
    }

    public OrchestrationBuilder WithDataRecording()
    {
        _config.RecordSteps = true;
        return this;
    }

    public OrchestrationBuilder WithOnRuntimeEvent(Func<ChatRuntimeEvents, ValueTask> handler)
    {
        _config.OnRuntimeEvent = handler;
        return this;
    }

    public OrchestrationBuilder AddAdvancer<T>(OrchestrationRunnableBase fromRunnable, OrchestrationRunnableBase toRunnable)
    {
        // Use dynamic to call AddAdvancer on the generic OrchestrationRunnable<TInput, TOutput>
        dynamic dynRunnable = fromRunnable;
        dynRunnable.AddAdvancer(toRunnable);
        return this;
    }

    public OrchestrationBuilder AddAdvancer<T>(OrchestrationRunnableBase fromRunnable, AdvancementRequirement<T> condition, OrchestrationRunnableBase toRunnable)
    {
        // Use dynamic to call AddAdvancer on the generic OrchestrationRunnable<TInput, TOutput>
        dynamic dynRunnable = fromRunnable;
        dynRunnable.AddAdvancer(condition, toRunnable);
        return this;
    }

    public OrchestrationBuilder AddAdvancers<T>(OrchestrationRunnableBase fromRunnable, params OrchestrationAdvancer<T>[] advancers)
    {
        // Use reflection to access internal AddAdvancer method
        var method = typeof(OrchestrationRunnableBase).GetMethod("AddAdvancer", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, new[] { typeof(OrchestrationAdvancer<T>) }, null);
        foreach (var advancer in advancers)
        {
            method?.Invoke(fromRunnable, new object[] { advancer });
        }
        return this;
    }

    public OrchestrationBuilder AddParallelAdvancement(OrchestrationRunnableBase fromRunnable, params OrchestrationAdvancer[] advancers)
    {
        fromRunnable.AllowsParallelAdvances = true;
        // Use reflection to access internal AddAdvancer method
        var method = typeof(OrchestrationRunnableBase).GetMethod("AddAdvancer", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, new[] { typeof(OrchestrationAdvancer) }, null);
        foreach (var advancer in advancers)
        {
            method?.Invoke(fromRunnable, new object[] { advancer });
        }
        return this;
    }

    public OrchestrationBuilder AddCombinationalAdvancement<T>(
        OrchestrationRunnableBase[] fromRunnables,
        AdvancementRequirement<T> condition,
        OrchestrationRunnableBase toRunnable,
        int requiredInputToAdvance,
        string combinationRunnableName)
    {
        // Create a combinational runnable that waits for all inputs
        // This is a simplified implementation - in practice, you'd need a CombinationalRunnable class
        // For now, we'll create a simple wrapper that combines results
        var combinationalRunnable = new CombinationalRunnable<T>(_config, combinationRunnableName, fromRunnables, requiredInputToAdvance);
        
        // Add the combinational runnable to the config
        _config.Runnables[combinationRunnableName] = combinationalRunnable;
        
        // Wire each fromRunnable to the combinational runnable
        foreach (var fromRunnable in fromRunnables)
        {
            dynamic dynRunnable = fromRunnable;
            dynRunnable.AddAdvancer(combinationalRunnable);
        }
        
        // Wire combinational runnable to the target
        dynamic dynCombinational = combinationalRunnable;
        dynCombinational.AddAdvancer(condition, toRunnable);
        
        return this;
    }

    public OrchestrationBuilder AddExitPath<T>(OrchestrationRunnableBase runnable, AdvancementRequirement<T> condition)
    {
        runnable.AllowDeadEnd = true;
        dynamic dynRunnable = runnable;
        dynRunnable.AddAdvancer(condition, runnable); // Self-loop for exit
        return this;
    }

    public OrchestrationBuilder CreateDotGraphVisualization(string filename)
    {
        // Create DOT graph visualization
        try
        {
            var dotContent = GenerateDotGraph();
            File.WriteAllText(filename, dotContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to create DOT graph visualization: {ex.Message}");
        }
        return this;
    }

    public OrchestrationBuilder CreatePlantUmlVisualization(string filename)
    {
        // Create PlantUML visualization (stub for now)
        try
        {
            var pumlContent = GeneratePlantUmlGraph();
            File.WriteAllText(filename, pumlContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to create PlantUML graph visualization: {ex.Message}");
        }
        return this;
    }

    public OrchestrationRuntimeConfiguration Build()
    {
        return _config;
    }

    private string GenerateDotGraph()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("digraph OrchestrationGraph {");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [shape=box];");
        
        // Add nodes
        foreach (var runnable in _config.Runnables.Values)
        {
            var label = runnable.RunnableName.Replace("\"", "\\\"");
            sb.AppendLine($"  \"{runnable.Id}\" [label=\"{label}\"];");
        }
        
        // Add entry node
        if (_config.InitialRunnable != null)
        {
            sb.AppendLine($"  \"ENTRY\" -> \"{_config.InitialRunnable.Id}\";");
        }
        
        // Add edges from advancers
        foreach (var runnable in _config.Runnables.Values)
        {
            foreach (var advancer in runnable.BaseAdvancers)
            {
                sb.AppendLine($"  \"{runnable.Id}\" -> \"{advancer.NextRunnable.Id}\";");
            }
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GeneratePlantUmlGraph()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("@startuml");
        
        // Add nodes
        foreach (var runnable in _config.Runnables.Values)
        {
            var label = runnable.RunnableName.Replace("\"", "\\\"");
            sb.AppendLine($"  [{label}]");
        }
        
        // Add edges
        foreach (var runnable in _config.Runnables.Values)
        {
            foreach (var advancer in runnable.BaseAdvancers)
            {
                var fromLabel = runnable.RunnableName.Replace("\"", "\\\"");
                var toLabel = advancer.NextRunnable.RunnableName.Replace("\"", "\\\"");
                sb.AppendLine($"  [{fromLabel}] --> [{toLabel}]");
            }
        }
        
        sb.AppendLine("@enduml");
        return sb.ToString();
    }

    /// <summary>
    /// Simple combinational runnable that waits for multiple inputs before advancing.
    /// </summary>
    private class CombinationalRunnable<T> : OrchestrationRunnable<object, CombinationalResult<T>>
    {
        private readonly OrchestrationRunnableBase[] _sourceRunnables;
        private readonly int _requiredCount;
        private readonly Dictionary<string, T> _collectedResults = new();

        public CombinationalRunnable(
            Orchestration orchestrator,
            string name,
            OrchestrationRunnableBase[] sourceRunnables,
            int requiredCount)
            : base(orchestrator, name)
        {
            _sourceRunnables = sourceRunnables ?? throw new ArgumentNullException(nameof(sourceRunnables));
            _requiredCount = requiredCount;
        }

        public override ValueTask<CombinationalResult<T>> Invoke(RunnableProcess<object, CombinationalResult<T>> process)
        {
            // This is a simplified implementation
            // In practice, you'd need to track which runnables have completed
            // and combine their results when all are ready
            
            // For now, we'll create a result with empty values
            // The actual implementation would need to track state across multiple invocations
            var result = new CombinationalResult<T>
            {
                Values = Array.Empty<T>()
            };
            
            return ValueTask.FromResult(result);
        }
    }
}

