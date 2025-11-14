using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Represents a checkpoint of orchestration state that can be serialized and resumed later.
/// </summary>
/// <typeparam name="TState">The type of state used by the orchestration.</typeparam>
public class OrchestrationCheckpoint<TState> where TState : class, IOrchestrationState
{
    /// <summary>
    /// Gets or sets the step number when this checkpoint was created.
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// Gets or sets the state snapshot at the time of checkpoint.
    /// </summary>
    public TState State { get; set; } = null!;

    /// <summary>
    /// Gets or sets the list of runnable IDs that were currently executing.
    /// </summary>
    public List<string> CurrentRunnableIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when this checkpoint was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Serializes this checkpoint to JSON.
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes a checkpoint from JSON.
    /// </summary>
    public static OrchestrationCheckpoint<TState> FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Deserialize<OrchestrationCheckpoint<TState>>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize checkpoint.");
    }
}

