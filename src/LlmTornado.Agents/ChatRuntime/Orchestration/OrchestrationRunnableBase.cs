using LlmTornado.Agents.DataModels;
using System.Runtime.ConstrainedExecution;

namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Represents the base class for defining a state within a state machine.
/// </summary>
/// <remarks>The <see cref="OrchestrationRunnableBase"/> class provides a framework for managing state transitions, input
/// and output processing, and state invocation within a state machine. It includes properties and methods for
/// handling state-specific logic, such as entering and exiting states, checking conditions, and managing
/// transitions.</remarks>
public abstract class OrchestrationRunnableBase 
{

    /// [SerializationRequired]
    internal List<object> baseResults  => BaseLastFinishedProcesses.Where(process => process.BaseResult != null).Select(process => process.BaseResult!).ToList();

    /// <summary>
    /// Results process that the state has to process this tick.
    /// </summary>
    /// [SerializationRequired]
    public List<RunnableProcess> BaseLastFinishedProcesses { get; set; } = [];

    public string RunnableName { get; set; } = "Runnable";

    /// <summary>
    /// Used to limit the number of times to rerun the state.
    /// </summary>
    public bool BeingReran = false;

    /// <summary>
    /// State identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();


    /// <summary>
    /// Input processes that the state has to process this tick.
    /// </summary>
    /// [SerializationRequired]
    public List<RunnableProcess> BaseProcesses { get; set; } = [];

    /// <summary>
    /// List of transitions that can be made from this state.
    /// </summary>
    public List<OrchestrationAdvancer> BaseAdvancers { get; set; } = [];

    /// <summary>
    /// State machine running the state
    /// </summary>
    public Orchestration? Orchestrator { get; set; }


    /// <summary>
    /// Gets or sets the <see cref="CancellationTokenSource"/> used to signal cancellation requests.
    /// </summary>
    public CancellationTokenSource cts { get; set; } = new CancellationTokenSource();

    /// <summary>
    /// Internal invoke to abstract BaseState from Type specifics.
    /// </summary>
    /// <returns></returns>
    /// 
    internal abstract ValueTask Invoke();

    /// <summary>
    /// initlizies the current state asynchronously.
    /// </summary>
    /// <remarks>This method is abstract and must be implemented by derived classes to define the
    /// specific behavior of entering a new state.</remarks>
    /// transition.</param>
    /// <returns></returns>
    internal abstract ValueTask _InitializeRunnable();

    /// <summary>
    /// Transitions the current state to an exit state asynchronously.
    /// </summary>
    /// <remarks>This method should be implemented to handle any necessary cleanup or finalization
    /// tasks when exiting a state. It is called as part of the state transition process.</remarks>
    /// <returns>A task that represents the asynchronous operation of exiting the state.</returns>
    internal abstract ValueTask _CleanupRunnable();

    /// <summary>
    /// Retrieves the type of input that this state can process.
    /// </summary>
    /// <returns>A <see cref="Type"/> object representing the input type that this instance is designed to handle.</returns>
    public abstract Type GetInputType();
    /// <summary>
    /// Gets the output type produced by this state.
    /// </summary>
    /// <returns>A <see cref="Type"/> representing the output type.</returns>
    public abstract Type GetOutputType();

    /// <summary>
    /// Gets or sets a value indicating whether parallel transitions are allowed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="true"/>, multiple advancers can be evaluated simultaneously, and all matching
    /// advancers will create new processes in parallel. This enables fan-out patterns where a single runnable
    /// can advance to multiple next runnables concurrently.
    /// </para>
    /// <para>
    /// When <see langword="false"/> (default), advancers are evaluated in order, and only the first matching
    /// advancer creates a process. This provides sequential, deterministic execution.
    /// </para>
    /// <para>
    /// Example - Parallel advancement:
    /// <code>
    /// runnable.AllowsParallelAdvances = true;
    /// runnable.AddAdvancer(condition1, nextRunnable1);
    /// runnable.AddAdvancer(condition2, nextRunnable2);
    /// // If both conditions match, both nextRunnables will receive processes
    /// </code>
    /// </para>
    /// <para>
    /// Example - Sequential advancement (default):
    /// <code>
    /// runnable.AllowsParallelAdvances = false; // Default
    /// runnable.AddAdvancer(condition1, nextRunnable1);
    /// runnable.AddAdvancer(condition2, nextRunnable2);
    /// // Only the first matching condition creates a process
    /// </code>
    /// </para>
    /// </remarks>
    public bool AllowsParallelAdvances { get; set; } = false;

    /// <summary>
    /// property to combine input into a single process to avoid running multiple threads for each input.
    /// </summary>
    public bool SingleInvokeForProcesses { get; set; } = false;

    /// <summary>
    /// Used to set if this state is okay not to have transitions.
    /// </summary>
    public bool AllowDeadEnd { get; set; } = false;

    /// <summary>
    /// Maximum number of times to rerun the state if conditions are not met.
    /// </summary>
    public int MaxReruns { get; set; } = 3;


    /// <summary>
    /// Evaluates and returns a list of runtime processes that meet specific conditions.
    /// </summary>
    /// <returns>A list of <see cref="RunnableProcess"/> objects that satisfy the defined conditions.  The list will be empty if
    /// no conditions are met.</returns>
    internal abstract List<RunnableProcess>? CanAdvance();

    /// <summary>
    /// Cancels the execution of the current state and any associated operations.
    /// </summary>
    public void Cancel()
    {
        cts.Cancel();
    }

    internal void AddAdvancer(OrchestrationAdvancer advancer)
    {
        BaseAdvancers.Add(advancer);
    }

    internal void AddAdvancer<TOutput>(OrchestrationAdvancer<TOutput> advancer)
    {
        BaseAdvancers.Add(advancer);
    }

    internal void AddAdvancer<TValue, TOutput>(OrchestrationAdvancer<TValue, TOutput> advancer)
    {
        BaseAdvancers.Add(advancer);
    }

    internal object[] GetBaseResults()
    {
        return baseResults.ToArray();
    }

    internal List<RunnableProcess> GetRunnableProcesses()
    {
        return BaseProcesses;
    }

    internal List<RunnableProcess<TInput, TOutput>> GetBaseRunnableProcesses<TInput, TOutput>() => BaseProcesses.Select(process => process.CloneProcess<TInput, TOutput>()).ToList();


    public void AddRunnableProcess(RunnableProcess process)
    {
       BaseProcesses.Add(process);
    }

    public void AddRunnableProcess<TInput, TOutput>(RunnableProcess<TInput, TOutput> process)
    {
        BaseProcesses.Add(process);
    }

    internal void UpdateBaseRunnableProcess(string id, RunnableProcess result)
    {
       for (int i = 0; i < BaseProcesses.Count; i++)
       {
           if (BaseProcesses[i].Id == id)
           {
               BaseProcesses[i] = RunnableProcess.CloneProcess(result);
               break;
           }
       }
    }

    internal void ClearAllProcesses()
    {
        BaseProcesses.Clear();
    }

    internal void ClearResults()
    {
        BaseLastFinishedProcesses.Clear();
    }

    internal void AddRangeBaseResults(RunnableProcess[] results)
    {
        BaseLastFinishedProcesses.AddRange(results);
    }
}