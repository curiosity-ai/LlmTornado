namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Configuration options for orchestration execution behavior.
/// </summary>
public class OrchestrationExecutionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether null inputs to advancers are allowed.
    /// When false (default), null inputs will throw <see cref="OrchestrationNullInputException"/>.
    /// When true, null inputs will be handled by the <see cref="NullHandler"/> delegate if provided.
    /// </summary>
    public bool AllowNullInputs { get; set; } = false;

    /// <summary>
    /// Gets or sets a custom handler for null inputs when <see cref="AllowNullInputs"/> is true.
    /// If null, null inputs will be treated as not advancing (returns false).
    /// </summary>
    public Func<object?, bool>? NullHandler { get; set; }

    /// <summary>
    /// Gets or sets the checkpointing configuration.
    /// If null, checkpointing is disabled.
    /// </summary>
    public CheckpointingOptions? Checkpointing { get; set; }
}

/// <summary>
/// Configuration options for checkpointing functionality.
/// </summary>
public class CheckpointingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether checkpointing is enabled.
    /// </summary>
    public bool EnableCheckpointing { get; set; } = false;

    /// <summary>
    /// Gets or sets the directory where checkpoint files will be saved.
    /// </summary>
    public string? CheckpointDirectory { get; set; }

    /// <summary>
    /// Gets or sets a custom serializer function for checkpoint state.
    /// If null, default JSON serialization will be used.
    /// </summary>
    public Func<object, string>? CheckpointSerializer { get; set; }
}

