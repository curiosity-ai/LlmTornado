using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LlmTornado.Codex;

/// <summary>
/// A conversation hosted by the Codex app-server, accepting text and images.
/// </summary>
public sealed class CodexThread
{
    private readonly CodexSession session;

    internal CodexThread(CodexSession session, string id, string? model)
    {
        this.session = session;
        Id = id;
        Model = model;
    }

    /// <summary>
    /// Codex thread identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Model selected by the app-server.
    /// </summary>
    public string? Model { get; }

    /// <summary>
    /// Runs one text Codex turn and waits for completion.
    /// </summary>
    public Task<CodexTurnResult> RunAsync(
        string input,
        CodexTurnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Codex input cannot be empty.", nameof(input));
        }

        return RunAsync(new[] { CodexInput.Text(input) }, options, cancellationToken);
    }

    /// <summary>
    /// Runs a Codex turn with ordered text and image parts and waits for completion.
    /// </summary>
    public Task<CodexTurnResult> RunAsync(
        IReadOnlyList<CodexInput> input,
        CodexTurnOptions? options = null,
        CancellationToken cancellationToken = default)
        => session.RunTurnAsync(Id, CodexInput.ToAppServerInput(input), options ?? new CodexTurnOptions(), cancellationToken);
}
