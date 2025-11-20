using System.Collections.Generic;
using System.Collections.Immutable;

namespace LlmTornado.Code.DiffMatchPatch;

/// <summary>
/// Compatibility layer for https://github.com/google/diff-match-patch
/// </summary>
internal static class DiffMatchPatch
{
    /// <summary>
    /// Parse a textual representation of patches and return a List of Patch
    /// objects.</summary>
    /// <param name="textline"></param>
    /// <param name="format">The patch format to expect. If AutoDetect, will attempt to infer from content.</param>
    /// <returns></returns>
    public static ImmutableList<Patch> PatchFromText(string textline, PatchFormat format = PatchFormat.AutoDetect)
    {
        return PatchList.Parse(textline, format);
    }

    /// <summary>
    /// Merge a set of patches onto the text.  Return a patched text, as well
    /// as an array of true/false values indicating which patches were applied.</summary>
    /// <param name="patches"></param>
    /// <param name="text">Old text</param>
    /// <returns>Two element Object array, containing the new text and an array of
    ///  bool values.</returns>
    public static (string newText, bool[] results) PatchApply(IEnumerable<Patch> patches, string text)
    {
        return patches.Apply(text);
    }
}