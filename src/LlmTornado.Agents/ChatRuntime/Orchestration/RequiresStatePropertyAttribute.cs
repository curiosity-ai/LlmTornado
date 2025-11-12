namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Attribute that marks a runnable's Invoke method as requiring access to a specific state property.
/// This attribute is used during runtime-compilation to validate that all required state properties exist.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to the <see cref="OrchestrationRunnable{TInput, TOutput}.Invoke"/> method
/// to declare which state properties the runnable accesses.
/// </para>
/// <para>
/// Example:
/// <code>
/// [RequiresStateProperty(nameof(MemeState.Theme))]
/// [RequiresStateProperty(nameof(MemeState.Api))]
/// public override ValueTask&lt;string&gt; Invoke(RunnableProcess&lt;ChatMessage, string&gt; process)
/// {
///     var state = GetState&lt;MemeState&gt;();
///     return ValueTask.FromResult(state.Theme);
/// }
/// </code>
/// </para>
/// <para>
/// During compilation, the validator will check that all properties specified by these attributes
/// exist in the state type and are properly initialized.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class RequiresStatePropertyAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the required state property.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresStatePropertyAttribute"/> class.
    /// </summary>
    /// <param name="propertyName">The name of the required state property. Use nameof() for type safety.</param>
    public RequiresStatePropertyAttribute(string propertyName)
    {
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
    }
}

