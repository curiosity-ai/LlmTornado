namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Represents the result of graph validation during runtime-compilation.
/// </summary>
public class GraphValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the graph validation passed.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets the collection of validation errors found during compilation.
    /// </summary>
    public List<ValidationError> Errors { get; } = new();

    /// <summary>
    /// Gets the collection of validation warnings found during compilation.
    /// </summary>
    public List<ValidationWarning> Warnings { get; } = new();

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    public void AddError(string message, string? suggestion = null, OrchestrationRunnableBase? runnable = null, GraphEdge? edge = null)
    {
        Errors.Add(new ValidationError(message, suggestion, runnable, edge));
        IsValid = false;
    }

    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    public void AddWarning(string message, string? suggestion = null, OrchestrationRunnableBase? runnable = null, GraphEdge? edge = null)
    {
        Warnings.Add(new ValidationWarning(message, suggestion, runnable, edge));
    }
}

/// <summary>
/// Represents a validation error found during graph compilation.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets the error message describing what went wrong.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets an optional suggestion for how to fix the error.
    /// </summary>
    public string? Suggestion { get; }

    /// <summary>
    /// Gets the runnable associated with this error, if any.
    /// </summary>
    public OrchestrationRunnableBase? Runnable { get; }

    /// <summary>
    /// Gets the edge associated with this error, if any.
    /// </summary>
    public GraphEdge? Edge { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    public ValidationError(string message, string? suggestion = null, OrchestrationRunnableBase? runnable = null, GraphEdge? edge = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Suggestion = suggestion;
        Runnable = runnable;
        Edge = edge;
    }

    /// <summary>
    /// Returns a formatted string representation of the error.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string> { Message };
        
        if (Runnable != null)
        {
            parts.Add($"Runnable: {Runnable.RunnableName}");
        }
        
        if (Edge != null)
        {
            parts.Add($"Edge: {Edge.SourceRunnable.RunnableName} → {Edge.TargetRunnable.RunnableName}");
        }
        
        if (!string.IsNullOrEmpty(Suggestion))
        {
            parts.Add($"Suggestion: {Suggestion}");
        }
        
        return string.Join(" | ", parts);
    }
}

/// <summary>
/// Represents a validation warning found during graph compilation.
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Gets the warning message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets an optional suggestion for addressing the warning.
    /// </summary>
    public string? Suggestion { get; }

    /// <summary>
    /// Gets the runnable associated with this warning, if any.
    /// </summary>
    public OrchestrationRunnableBase? Runnable { get; }

    /// <summary>
    /// Gets the edge associated with this warning, if any.
    /// </summary>
    public GraphEdge? Edge { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationWarning"/> class.
    /// </summary>
    public ValidationWarning(string message, string? suggestion = null, OrchestrationRunnableBase? runnable = null, GraphEdge? edge = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Suggestion = suggestion;
        Runnable = runnable;
        Edge = edge;
    }
}

/// <summary>
/// Exception thrown when graph compilation fails due to validation errors.
/// </summary>
public class OrchestrationCompilationException : Exception
{
    /// <summary>
    /// Gets the validation result containing all errors and warnings.
    /// </summary>
    public GraphValidationResult ValidationResult { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationCompilationException"/> class.
    /// </summary>
    public OrchestrationCompilationException(GraphValidationResult validationResult)
        : base(BuildExceptionMessage(validationResult))
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }

    private static string BuildExceptionMessage(GraphValidationResult validationResult)
    {
        var message = new System.Text.StringBuilder();
        message.AppendLine($"Graph compilation failed with {validationResult.Errors.Count} error(s) and {validationResult.Warnings.Count} warning(s).");
        
        if (validationResult.Errors.Count > 0)
        {
            message.AppendLine("\nErrors:");
            for (int i = 0; i < validationResult.Errors.Count; i++)
            {
                message.AppendLine($"  {i + 1}. {validationResult.Errors[i]}");
            }
        }
        
        if (validationResult.Warnings.Count > 0)
        {
            message.AppendLine("\nWarnings:");
            for (int i = 0; i < validationResult.Warnings.Count; i++)
            {
                var warning = validationResult.Warnings[i];
                var warningParts = new List<string> { warning.Message };
                
                if (warning.Runnable != null)
                {
                    warningParts.Add($"Runnable: {warning.Runnable.RunnableName}");
                }
                
                if (warning.Edge != null)
                {
                    warningParts.Add($"Edge: {warning.Edge.SourceRunnable.RunnableName} → {warning.Edge.TargetRunnable.RunnableName}");
                }
                
                if (!string.IsNullOrEmpty(warning.Suggestion))
                {
                    warningParts.Add($"Suggestion: {warning.Suggestion}");
                }
                
                message.AppendLine($"  {i + 1}. {string.Join(" | ", warningParts)}");
            }
        }
        
        return message.ToString();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationCompilationException"/> class with a specific message.
    /// </summary>
    public OrchestrationCompilationException(string message, GraphValidationResult validationResult)
        : base(message)
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }
}

/// <summary>
/// Exception thrown when a null input is provided to an advancer and null inputs are not allowed.
/// </summary>
public class OrchestrationNullInputException : Exception
{
    /// <summary>
    /// Gets the runnable that produced the null output.
    /// </summary>
    public OrchestrationRunnableBase? Runnable { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationNullInputException"/> class.
    /// </summary>
    public OrchestrationNullInputException(string message, OrchestrationRunnableBase? runnable = null)
        : base(message)
    {
        Runnable = runnable;
    }
}

/// <summary>
/// Exception thrown when checkpoint operations fail.
/// </summary>
public class OrchestrationCheckpointException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationCheckpointException"/> class.
    /// </summary>
    public OrchestrationCheckpointException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationCheckpointException"/> class.
    /// </summary>
    public OrchestrationCheckpointException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

