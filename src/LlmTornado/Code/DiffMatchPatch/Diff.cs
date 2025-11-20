using System;
using System.Collections.Immutable;
using System.Threading;

namespace LlmTornado.Code.DiffMatchPatch;

public readonly record struct Diff(DiffOperation DiffOperation, string Text)
{
    internal static Diff Create(DiffOperation diffOperation, string text) => new Diff(diffOperation, text);
    public static Diff Equal(ReadOnlySpan<char> text) => Create(DiffOperation.Equal, text.ToString());
    public static Diff Insert(ReadOnlySpan<char> text) => Create(DiffOperation.Insert, text.ToString());
    public static Diff Delete(ReadOnlySpan<char> text) => Create(DiffOperation.Delete, text.ToString());
    public static Diff Empty => new Diff(DiffOperation.Equal, string.Empty);
    /// <summary>
    /// Generate a human-readable version of this Diff.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        string prettyText = Text.Replace('\n', '\u00b6');
        return "Diff(" + DiffOperation + ",\"" + prettyText + "\")";
    }

    internal Diff Replace(string text) => this with { Text = text };
    internal Diff Append(string text) => this with { Text = Text + text };
    internal Diff Prepend(string text) => this with { Text = text + Text };

    public bool IsEmpty => Text.Length == 0;

    /// <summary>
    /// Find the differences between two texts.
    /// </summary>
    /// <param name="text1">Old string to be diffed</param>
    /// <param name="text2">New string to be diffed</param>
    /// <param name="timeoutInSeconds">if specified, certain optimizations may be enabled to meet the time constraint, possibly resulting in a less optimal diff</param>
    /// <param name="checklines">If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
    /// <returns></returns>
    public static ImmutableList<Diff> Compute(string text1, string text2, float timeoutInSeconds = 0f, bool checklines = true)
    {
        using CancellationTokenSource cts = timeoutInSeconds <= 0
            ? new CancellationTokenSource()
            : new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds));
        return Compute(text1, text2, checklines, timeoutInSeconds > 0, cts.Token);
    }

    public static ImmutableList<Diff> Compute(string text1, string text2, bool checkLines, bool optimizeForSpeed, CancellationToken token)
        => DiffAlgorithm.Compute(text1, text2, checkLines, optimizeForSpeed, token).ToImmutableList();

    public bool IsLargeDelete(int size) => DiffOperation == DiffOperation.Delete && Text.Length > size;

}
