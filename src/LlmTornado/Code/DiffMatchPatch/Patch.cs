using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using static LlmTornado.Code.DiffMatchPatch.DiffOperation;

namespace LlmTornado.Code.DiffMatchPatch;

public record Patch(int Start1, int Length1, int Start2, int Length2, ImmutableListWithValueSemantics<Diff> Diffs)
{
    public Patch Bump(int length) => this with { Start1 = Start1 + length, Start2 = Start2 + length };

    public bool IsEmpty => Diffs.IsEmpty;
    public bool StartsWith(DiffOperation diffOperation) => Diffs[0].DiffOperation == diffOperation;
    public bool EndsWith(DiffOperation diffOperation) => Diffs[^1].DiffOperation == diffOperation;

    internal Patch AddPaddingInFront(string padding)
    {
        (int s1, int l1, int s2, int l2, ImmutableListWithValueSemantics<Diff> diffs) = this;

        ImmutableList<Diff>.Builder builder = diffs.ToBuilder();
        (s1, l1, s2, l2) = AddPaddingInFront(builder, s1, l1, s2, l2, padding);

        return new Patch(s1, l1, s2, l2, builder.ToImmutable());
    }

    internal Patch AddPaddingAtEnd(string padding)
    {
        (int s1, int l1, int s2, int l2, ImmutableListWithValueSemantics<Diff> diffs) = this;

        ImmutableList<Diff>.Builder builder = diffs.ToBuilder();
        (s1, l1, s2, l2) = AddPaddingAtEnd(builder, s1, l1, s2, l2, padding);

        return new Patch(s1, l1, s2, l2, builder.ToImmutable());
    }

    internal Patch AddPadding(string padding)
    {
        (int s1, int l1, int s2, int l2, ImmutableListWithValueSemantics<Diff> diffs) = this;

        ImmutableList<Diff>.Builder builder = diffs.ToBuilder();
        (s1, l1, s2, l2) = AddPaddingInFront(builder, s1, l1, s2, l2, padding);
        (s1, l1, s2, l2) = AddPaddingAtEnd(builder, s1, l1, s2, l2, padding);

        return new Patch(s1, l1, s2, l2, builder.ToImmutable());
    }

    private (int s1, int l1, int s2, int l2) AddPaddingInFront(ImmutableList<Diff>.Builder builder, int s1, int l1, int s2, int l2, string padding)
    {
        if (!StartsWith(Equal))
        {
            builder.Insert(0, Diff.Equal(padding));
            return (s1 - padding.Length, l1 + padding.Length, s2 - padding.Length, l2 + padding.Length);
        }

        if (padding.Length > Diffs[0].Text.Length)
        {
            Diff firstDiff = Diffs[0];
            int extraLength = padding.Length - firstDiff.Text.Length;
            string text = padding[firstDiff.Text.Length..] + firstDiff.Text;

            builder.RemoveAt(0);
            builder.Insert(0, firstDiff.Replace(text));
            return (s1 - extraLength, l1 + extraLength, s2 - extraLength, l2 + extraLength);
        }

        return (s1, l1, s2, l2);

    }

    private (int s1, int l1, int s2, int l2) AddPaddingAtEnd(ImmutableList<Diff>.Builder builder, int s1, int l1, int s2, int l2, string padding)
    {
        if (!EndsWith(Equal))
        {
            builder.Add(Diff.Equal(padding));
            return (s1, l1 + padding.Length, s2, l2 + padding.Length);
        }

        if (padding.Length > Diffs[^1].Text.Length)
        {
            Diff lastDiff = Diffs[^1];
            int extraLength = padding.Length - lastDiff.Text.Length;
            string text = lastDiff.Text + padding[..extraLength];

            builder.RemoveAt(builder.Count - 1);
            builder.Add(lastDiff.Replace(text));

            return (s1, l1 + extraLength, s2, l2 + extraLength);
        }

        return (s1, l1, s2, l2);

    }

    /// <summary>
    /// Generate GNU diff's format.
    /// Header: @@ -382,8 +481,9 @@
    /// Indicies are printed as 1-based, not 0-based.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {

        string coords1 = Length1 switch
        {
            0 => Start1 + ",0",
            1 => Convert.ToString(Start1 + 1),
            _ => Start1 + 1 + "," + Length1
        };

        string coords2 = Length2 switch
        {
            0 => Start2 + ",0",
            1 => Convert.ToString(Start2 + 1),
            _ => Start2 + 1 + "," + Length2
        };

        StringBuilder text = new StringBuilder()
            .Append("@@ -")
            .Append(coords1)
            .Append(" +")
            .Append(coords2)
            .Append(" @@\n");

        // Escape the body of the patch with %xx notation.
        foreach (Diff aDiff in Diffs)
        {
            text.Append((char)aDiff.DiffOperation);
            text.Append(aDiff.Text.UrlEncoded()).Append("\n");
        }

        return text.ToString();
    }
    /// <summary>
    /// Compute a list of patches to turn text1 into text2.
    /// A set of Diffs will be computed.
    /// </summary>
    /// <param name="text1">old text</param>
    /// <param name="text2">new text</param>
    /// <param name="diffTimeout">timeout in seconds</param>
    /// <param name="diffEditCost">Cost of an empty edit operation in terms of edit characters.</param>
    /// <returns>List of Patch objects</returns>
    public static ImmutableListWithValueSemantics<Patch> Compute(string text1, string text2, float diffTimeout = 0, short diffEditCost = 4)
    {
        using (CancellationTokenSource cts = diffTimeout <= 0
                   ? new CancellationTokenSource()
                   : new CancellationTokenSource(TimeSpan.FromSeconds(diffTimeout)))
        {
            return Compute(text1, DiffAlgorithm.Compute(text1, text2, true, true, cts.Token).CleanupSemantic().CleanupEfficiency(diffEditCost)).ToImmutableList().WithValueSemantics();
        }
    }

    /// <summary>
    /// Compute a list of patches to turn text1 into text2.
    /// text1 will be derived from the provided Diffs.
    /// </summary>
    /// <param name="diffs">array of diff objects for text1 to text2</param>
    /// <returns>List of Patch objects</returns>
    public static ImmutableListWithValueSemantics<Patch> FromDiffs(IEnumerable<Diff> diffs)
        => Compute(diffs.Text1(), diffs).ToImmutableList().WithValueSemantics();

    /// <summary>
    /// Compute a list of patches to turn text1 into text2.
    /// text2 is not provided, Diffs are the delta between text1 and text2.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="diffs"></param>
    /// <param name="patchMargin"></param>
    /// <returns></returns>
    public static IEnumerable<Patch> Compute(string text1, IEnumerable<Diff> diffs, short patchMargin = 4)
    {
        if (!diffs.Any())
        {
            yield break;  // Get rid of the null case.
        }

        int charCount1 = 0;  // Number of characters into the text1 string.
        int charCount2 = 0;  // Number of characters into the text2 string.
                             // Start with text1 (prepatch_text) and apply the Diffs until we arrive at
                             // text2 (postpatch_text). We recreate the patches one by one to determine
                             // context info.
        string prepatchText = text1;
        string postpatchText = text1;
        ImmutableList<Diff>.Builder newdiffs = ImmutableList.CreateBuilder<Diff>();
        int start1 = 0, length1 = 0, start2 = 0, length2 = 0;
        foreach (Diff aDiff in diffs)
        {
            if (newdiffs.Count == 0 && aDiff.DiffOperation != Equal)
            {
                // A new patch starts here.
                start1 = charCount1;
                start2 = charCount2;
            }

            switch (aDiff.DiffOperation)
            {
                case Insert:
                    newdiffs.Add(aDiff);
                    length2 += aDiff.Text.Length;
                    postpatchText = postpatchText.Insert(charCount2, aDiff.Text);
                    break;
                case Delete:
                    length1 += aDiff.Text.Length;
                    newdiffs.Add(aDiff);
                    postpatchText = postpatchText.Remove(charCount2, aDiff.Text.Length);
                    break;
                case Equal:
                    if (aDiff.Text.Length <= 2 * patchMargin && newdiffs.Count != 0 && aDiff != diffs.Last())
                    {
                        // Small equality inside a patch.
                        newdiffs.Add(aDiff);
                        length1 += aDiff.Text.Length;
                        length2 += aDiff.Text.Length;
                    }

                    if (aDiff.Text.Length >= 2 * patchMargin)
                    {
                        // Time for a new patch.
                        if (newdiffs.Count != 0)
                        {
                            (start1, length1, start2, length2) = newdiffs.AddContext(prepatchText, start1, length1, start2, length2);
                            yield return new Patch(start1, length1, start2, length2, newdiffs.ToImmutable());
                            start1 = start2 = length1 = length2 = 0;
                            newdiffs.Clear();
                            // Unlike Unidiff, our patch lists have a rolling context.
                            // http://code.google.com/p/google-diff-match-patch/wiki/Unidiff
                            // Update prepatch text & pos to reflect the application of the
                            // just completed patch.
                            prepatchText = postpatchText;
                            charCount1 = charCount2;
                        }
                    }
                    break;
            }

            // Update the current character count.
            if (aDiff.DiffOperation != Insert)
            {
                charCount1 += aDiff.Text.Length;
            }
            if (aDiff.DiffOperation != Delete)
            {
                charCount2 += aDiff.Text.Length;
            }
        }
        // Pick up the leftover patch if not empty.
        if (newdiffs.Count != 0)
        {
            (start1, length1, start2, length2) = newdiffs.AddContext(prepatchText, start1, length1, start2, length2);
            yield return new Patch(start1, length1, start2, length2, newdiffs.ToImmutable());
        }
    }

}
