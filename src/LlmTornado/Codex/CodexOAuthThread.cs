using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.ChatFunctions;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Codex;

/// <summary>
/// A client-side thread using direct ChatGPT subscription OAuth, accepting text and images.
/// </summary>
public sealed class CodexOAuthThread
{
    private readonly CodexOAuthSession session;
    private readonly string baseInstructions;
    private readonly List<JObject> history = [];
    private readonly SemaphoreSlim turnLock = new SemaphoreSlim(1, 1);

    internal CodexOAuthThread(
        CodexOAuthSession session,
        string id,
        string model,
        string baseInstructions,
        string? developerInstructions,
        IReadOnlyList<CodexOAuthHistoryItem>? initialHistory)
    {
        this.session = session;
        this.baseInstructions = baseInstructions;
        Id = id;
        Model = model;

        if (!string.IsNullOrWhiteSpace(developerInstructions))
        {
            history.Add(CreateMessage("developer", developerInstructions!));
        }

        if (initialHistory is not null)
        {
            foreach (CodexOAuthHistoryItem item in initialHistory)
            {
                if (item is null)
                {
                    throw new ArgumentNullException(nameof(item));
                }

                history.Add(item.ToJson());
            }
        }
    }

    /// <summary>
    /// Client-side thread identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Default model selected for this thread.
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// Runs one text turn and waits for the streamed response to complete.
    /// </summary>
    public async Task<CodexOAuthTurnResult> RunAsync(
        string input,
        CodexOAuthTurnOptions? options = null,
        CancellationToken cancellationToken = default)
        => await RunAsync(new[] { CodexInput.Text(input) }, options, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Runs a turn with ordered text and image parts and waits for the streamed response to complete.
    /// </summary>
    public Task<CodexOAuthTurnResult> RunAsync(
        IReadOnlyList<CodexInput> input,
        CodexOAuthTurnOptions? options = null,
        CancellationToken cancellationToken = default)
        => RunAsyncCore(input, options, null, cancellationToken);

    /// <summary>
    /// Runs a turn, lets the host resolve requested function calls, and continues until final text is returned.
    /// </summary>
    public async Task<CodexOAuthTurnResult> RunAsync(
        string input,
        Func<List<FunctionCall>, ValueTask> functionCallHandler,
        CodexOAuthTurnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (functionCallHandler is null)
        {
            throw new ArgumentNullException(nameof(functionCallHandler));
        }

        return await RunAsync(new[] { CodexInput.Text(input) }, functionCallHandler, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a turn with a cancellation-aware host function-call handler and continues until final text is returned.
    /// </summary>
    public async Task<CodexOAuthTurnResult> RunAsync(
        string input,
        Func<List<FunctionCall>, CancellationToken, ValueTask> functionCallHandler,
        CodexOAuthTurnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (functionCallHandler is null)
        {
            throw new ArgumentNullException(nameof(functionCallHandler));
        }

        return await RunAsync(new[] { CodexInput.Text(input) }, functionCallHandler, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a text and image turn with a host function-call handler and continues to final text.
    /// </summary>
    public Task<CodexOAuthTurnResult> RunAsync(
        IReadOnlyList<CodexInput> input,
        Func<List<FunctionCall>, ValueTask> functionCallHandler,
        CodexOAuthTurnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (functionCallHandler is null)
        {
            throw new ArgumentNullException(nameof(functionCallHandler));
        }

        return RunAsyncCore(input, options, (calls, _) => functionCallHandler(calls), cancellationToken);
    }

    /// <summary>
    /// Runs a text and image turn with a cancellation-aware host handler and continues to final text.
    /// </summary>
    public Task<CodexOAuthTurnResult> RunAsync(
        IReadOnlyList<CodexInput> input,
        Func<List<FunctionCall>, CancellationToken, ValueTask> functionCallHandler,
        CodexOAuthTurnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (functionCallHandler is null)
        {
            throw new ArgumentNullException(nameof(functionCallHandler));
        }

        return RunAsyncCore(input, options, functionCallHandler, cancellationToken);
    }

    private async Task<CodexOAuthTurnResult> RunAsyncCore(
        IReadOnlyList<CodexInput> input,
        CodexOAuthTurnOptions? options,
        Func<List<FunctionCall>, CancellationToken, ValueTask>? functionCallHandler,
        CancellationToken cancellationToken)
    {
        CodexInput[] snapshot = CodexInput.Snapshot(input);
        await turnLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            JObject userMessage = await CodexInput.CreateUserMessageAsync(snapshot, cancellationToken).ConfigureAwait(false);
            options ??= new CodexOAuthTurnOptions();
            List<JObject> turnInput = history
                .Select(item => (JObject)item.DeepClone())
                .ToList();
            turnInput.Add(userMessage);
            List<JObject> newHistory = [userMessage];
            List<ToolCall> allToolCalls = [];

            for (int iteration = 0; iteration < 32; iteration++)
            {
                CodexOAuthTurnResult result = await session.RunTurnAsync(
                    Id,
                    Model,
                    baseInstructions,
                    turnInput,
                    options,
                    cancellationToken).ConfigureAwait(false);
                List<JObject> outputItems = result.OutputItems
                    .Select(item => (JObject)item.DeepClone())
                    .ToList();
                turnInput.AddRange(outputItems.Select(item => (JObject)item.DeepClone()));
                newHistory.AddRange(outputItems);
                allToolCalls.AddRange(result.ToolCalls);

                if (functionCallHandler is null || result.ToolCalls.Count == 0)
                {
                    history.AddRange(newHistory);

                    bool hasAssistantMessage = newHistory.Any(item =>
                        string.Equals(item.Value<string>("type"), "message", StringComparison.Ordinal)
                        && string.Equals(item.Value<string>("role"), "assistant", StringComparison.Ordinal));
                    if (!hasAssistantMessage)
                    {
                        history.Add(CreateMessage("assistant", result.FinalResponse, "output_text"));
                    }

                    return allToolCalls.Count == result.ToolCalls.Count
                        ? result
                        : new CodexOAuthTurnResult(
                            result.ThreadId,
                            result.ResponseId,
                            result.FinalResponse,
                            result.Status,
                            result.Response,
                            newHistory.Skip(1).ToList(),
                            allToolCalls);
                }

                List<FunctionCall> functionCalls = result.ToolCalls
                    .Select(call => call.FunctionCall)
                    .Where(call => call is not null)
                    .Cast<FunctionCall>()
                    .ToList();
                await functionCallHandler(functionCalls, cancellationToken).ConfigureAwait(false);

                foreach (FunctionCall call in functionCalls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    JObject output = new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = call.ToolCall?.Id ?? call.Name,
                        ["output"] = call.Result?.Content
                            ?? new JObject { ["error"] = "The tool call was not handled." }.ToString(Newtonsoft.Json.Formatting.None)
                    };
                    turnInput.Add((JObject)output.DeepClone());
                    newHistory.Add(output);
                }
            }

            throw new CodexOAuthException("Codex exceeded the maximum number of tool-call continuations.");
        }
        finally
        {
            turnLock.Release();
        }
    }

    private static JObject CreateMessage(string role, string text, string contentType = "input_text")
        => new JObject
        {
            ["type"] = "message",
            ["role"] = role,
            ["content"] = new JArray
            {
                new JObject
                {
                    ["type"] = contentType,
                    ["text"] = text
                }
            }
        };
}
