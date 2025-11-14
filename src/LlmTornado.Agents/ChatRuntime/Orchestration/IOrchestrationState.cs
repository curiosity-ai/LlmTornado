namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Marker interface for strongly-typed orchestration state.
/// Implement this interface on your state class to enable compile-time type safety
/// and runtime-compilation validation of state property access.
/// </summary>
/// <remarks>
/// <para>
/// Instead of using <see cref="Orchestration.RuntimeProperties"/> dictionary with string keys,
/// create a class that implements this interface to define your orchestration's shared state schema.
/// </para>
/// <para>
/// Example:
/// <code>
/// public class MemeOrchestrationState : IOrchestrationState
/// {
///     public string Theme { get; set; }
///     public TornadoApi Api { get; set; }
///     public int Iteration { get; set; }
/// }
/// </code>
/// </para>
/// <para>
/// Access state in runnables using <see cref="OrchestrationRunnableBase.GetState{TState}()"/>:
/// <code>
/// var state = GetState&lt;MemeOrchestrationState&gt;();
/// string theme = state.Theme; // Type-safe, no casting needed
/// </code>
/// </para>
/// </remarks>
public interface IOrchestrationState
{
}

