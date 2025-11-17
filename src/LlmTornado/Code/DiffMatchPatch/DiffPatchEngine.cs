using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LlmTornado.Code.DiffMatchPatch;

/// <summary>
/// Specifies the format of a patch/diff text.
/// </summary>
public enum PatchFormat
{
    /// <summary>
    /// Auto-detect the format based on markers and structure.
    /// This is the default but slower and less reliable than explicit format hints.
    /// </summary>
    AutoDetect = 0,
    
    /// <summary>
    /// Git diff format with @@ headers containing line numbers.
    /// Example: @@ -1,3 +1,4 @@
    /// </summary>
    Git = 1,
    
    /// <summary>
    /// OpenAI V4A format (headerless unified diff used by GPT-5.1's apply_patch tool).
    /// Uses @@ markers without line numbers, relies on context matching.
    /// Example: @@
    /// </summary>
    V4a = 2
}

/// <summary>
/// Public facade over the internal diff-match-patch implementation.
/// </summary>
public static class DiffPatchEngine
{
    /// <summary>
    /// Generates patches representing the transition from <paramref name="original"/> to <paramref name="updated"/>.
    /// </summary>
    public static IReadOnlyList<Patch> Generate(string original, string updated, float diffTimeoutSeconds = 0f, short diffEditCost = 4)
    {
        original ??= string.Empty;
        updated ??= string.Empty;

        return Patch.Compute(original, updated, diffTimeoutSeconds, diffEditCost);
    }

    /// <summary>
    /// Generates the textual diff (patch format) for the provided inputs.
    /// </summary>
    public static string GenerateDiffText(string original, string updated, float diffTimeoutSeconds = 0f, short diffEditCost = 4)
    {
        return Generate(original, updated, diffTimeoutSeconds, diffEditCost).ToText();
    }

    /// <summary>
    /// Parses unified diff text into patch objects.
    /// </summary>
    /// <param name="diffText">The diff text to parse.</param>
    /// <param name="format">The format of the diff. If AutoDetect, the parser will attempt to infer the format.</param>
    public static ImmutableList<Patch> Parse(string diffText, PatchFormat format = PatchFormat.AutoDetect)
    {
        if (diffText is null)
        {
            throw new ArgumentNullException(nameof(diffText));
        }

        return DiffMatchPatch.PatchFromText(diffText, format);
    }

    /// <summary>
    /// Applies a textual diff to the provided source text with explicit format specification.
    /// </summary>
    /// <param name="source">The original source text to patch.</param>
    /// <param name="diffText">The diff text to apply.</param>
    /// <param name="patchedText">The resulting patched text.</param>
    /// <param name="error">Error message if the operation fails.</param>
    /// <param name="format">The format of the diff. If AutoDetect, the parser will attempt to infer the format.</param>
    /// <param name="requireAllPatches">If true, returns false if any patch fails. If false, applies as many patches as possible.</param>
    public static bool TryApply(string source, string diffText, out string patchedText, out string? error, PatchFormat format, bool requireAllPatches = true)
    {
        source ??= string.Empty;

        try
        {
            ImmutableList<Patch> patches = Parse(diffText, format);
            return TryApply(source, patches, out patchedText, out error, requireAllPatches);
        }
        catch (Exception ex)
        {
            patchedText = source;
            error = $"Failed to parse patches: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// Applies a textual diff to the provided source text (auto-detects format).
    /// </summary>
    public static bool TryApply(string source, string diffText, out string patchedText, out string? error, bool requireAllPatches = true)
    {
        return TryApply(source, diffText, out patchedText, out error, PatchFormat.AutoDetect, requireAllPatches);
    }

    /// <summary>
    /// Applies parsed patches to the provided source text.
    /// </summary>
    public static bool TryApply(string source, IReadOnlyList<Patch> patches, out string patchedText, out string? error, bool requireAllPatches = true)
    {
        source ??= string.Empty;

        if (patches is null || patches.Count == 0)
        {
            patchedText = source;
            error = "No patches to apply.";
            return false;
        }

        try
        {
            ImmutableList<Patch> immutablePatches = patches as ImmutableList<Patch> ?? patches.ToImmutableList();
            (string newText, bool[] results) = DiffMatchPatch.PatchApply(immutablePatches, source);
            bool success = !requireAllPatches || results.All(x => x);

            patchedText = newText;
            error = success ? null : "One or more hunks failed to apply.";
            return success;
        }
        catch (Exception ex)
        {
            patchedText = source;
            error = ex.Message;
            return false;
        }
    }
}