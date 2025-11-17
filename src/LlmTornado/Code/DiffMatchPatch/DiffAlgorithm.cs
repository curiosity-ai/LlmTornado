using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static LlmTornado.Code.DiffMatchPatch.DiffOperation;

namespace LlmTornado.Code.DiffMatchPatch;

internal static class DiffAlgorithm
{
    /// <summary>
    /// Find the differences between two texts.  Simplifies the problem by
    /// stripping any common prefix or suffix off the texts before diffing.
    /// </summary>
    /// <param name="text1">Old string to be diffed.</param>
    /// <param name="text2">New string to be diffed.</param>
    /// <param name="checklines">Speedup flag.  If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
    /// <param name="optimizeForSpeed">Should optimizations be enabled?</param>
    /// <param name="token">Cancellation token for cooperative cancellation</param>
    /// <returns></returns>
    internal static IEnumerable<Diff> Compute(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2, bool checklines, bool optimizeForSpeed, CancellationToken token)
    {
        if (text1.Length == text2.Length && text1.Length == 0)
            return [];

        int commonlength = TextUtil.CommonPrefix(text1, text2);

        if (commonlength == text1.Length && commonlength == text2.Length)
        {
            // equal
            return new List<Diff>()
                {
                    Diff.Equal(text1)
                };
        }

        // Trim off common prefix (speedup).
        ReadOnlySpan<char> commonprefix = text1[..commonlength];
        text1 = text1[commonlength..];
        text2 = text2[commonlength..];

        // Trim off common suffix (speedup).
        commonlength = TextUtil.CommonSuffix(text1, text2);
        ReadOnlySpan<char> commonsuffix = text1[^commonlength..];
        text1 = text1[..^commonlength];
        text2 = text2[..^commonlength];

        List<Diff> diffs = [];
        // Compute the diff on the middle block.
        if (commonprefix.Length != 0)
        {
            diffs.Insert(0, Diff.Equal(commonprefix));
        }

        diffs.AddRange(ComputeImpl(text1, text2, checklines, optimizeForSpeed, token));

        if (commonsuffix.Length != 0)
        {
            diffs.Add(Diff.Equal(commonsuffix));
        }

        return diffs.CleanupMerge();
    }

    /// <summary>
    /// Find the differences between two texts.  Assumes that the texts do not
    /// have any common prefix or suffix.
    /// </summary>
    /// <param name="text1">Old string to be diffed.</param>
    /// <param name="text2">New string to be diffed.</param>
    /// <param name="checklines">Speedup flag.  If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
    /// <param name="optimizeForSpeed">Should optimizations be enabled?</param>
    /// <param name="token">Cancellation token for cooperative cancellation</param>
    /// <returns></returns>
    private static IEnumerable<Diff> ComputeImpl(
        ReadOnlySpan<char> text1,
        ReadOnlySpan<char> text2,
        bool checklines,
        bool optimizeForSpeed,
        CancellationToken token)
    {

        if (text1.Length == 0)
        {
            // Just add some text (speedup).
            return Diff.Insert(text2).ItemAsEnumerable();
        }

        if (text2.Length == 0)
        {
            // Just delete some text (speedup).
            return Diff.Delete(text1).ItemAsEnumerable();
        }

        ReadOnlySpan<char> longtext = text1.Length > text2.Length ? text1 : text2;
        ReadOnlySpan<char> shorttext = text1.Length > text2.Length ? text2 : text1;
        int i = longtext.IndexOf(shorttext, StringComparison.Ordinal);
        if (i != -1)
        {
            // Shorter text is inside the longer text (speedup).
            if (text1.Length > text2.Length)
            {
                return
                [
                    Diff.Delete(longtext[..i]),
                        Diff.Equal(shorttext),
                        Diff.Delete(longtext[(i + shorttext.Length)..])
                ];
            }

            return
            [
                Diff.Insert(longtext[..i]),
                Diff.Equal(shorttext),
                Diff.Insert(longtext[(i + shorttext.Length)..])
            ];
        }

        if (shorttext.Length == 1)
        {
            // Single character string.
            // After the previous speedup, the character can't be an equality.
            return
            [
                Diff.Delete(text1),
                    Diff.Insert(text2)
            ];
        }

        // Don't risk returning a non-optimal diff if we have unlimited time.
        if (optimizeForSpeed)
        {
            // Check to see if the problem can be split in two.
            HalfMatchResult result = TextUtil.HalfMatch(text1, text2);
            if (!result.IsEmpty)
            {
                // A half-match was found, sort out the return data.
                // Send both pairs off for separate processing.
                IEnumerable<Diff> diffsA = Compute(result.Prefix1, result.Prefix2, checklines, optimizeForSpeed, token);
                IEnumerable<Diff> diffsB = Compute(result.Suffix1, result.Suffix2, checklines, optimizeForSpeed, token);

                // Merge the results.
                return diffsA
                    .Concat(Diff.Equal(result.CommonMiddle))
                    .Concat(diffsB);
            }
        }
        if (checklines && text1.Length > 100 && text2.Length > 100)
        {
            return LineDiff(text1, text2, optimizeForSpeed, token);
        }

        return MyersDiffBisect(text1, text2, optimizeForSpeed, token);
    }

    /// <summary>
    /// Do a quick line-level diff on both strings, then rediff the parts for
    /// greater accuracy. This speedup can produce non-minimal Diffs.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <param name="optimizeForSpeed"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static List<Diff> LineDiff(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2, bool optimizeForSpeed, CancellationToken token)
    {
        // Scan the text on a line-by-line basis first.
        LineToCharCompressor compressor = new LineToCharCompressor();
        text1 = compressor.Compress(text1, char.MaxValue * 2 / 3);
        text2 = compressor.Compress(text2, char.MaxValue);
        List<Diff> diffs = Compute(text1, text2, false, optimizeForSpeed, token)
            .Select(diff => diff.Replace(compressor.Decompress(diff.Text)))
            .ToList()
            .CleanupSemantic(); // Eliminate freak matches (e.g. blank lines)

        return RediffAfterLineDiff(diffs, optimizeForSpeed, token).ToList();
    }

    // Rediff any replacement blocks, this time character-by-character.
    private static IEnumerable<Diff> RediffAfterLineDiff(IEnumerable<Diff> diffs, bool optimizeForSpeed, CancellationToken token)
    {
        StringBuilder ins = new StringBuilder();
        StringBuilder del = new StringBuilder();
        foreach (Diff diff in diffs.Concat(Diff.Empty))
        {
            (ins, del) = diff.DiffOperation switch
            {
                Insert => (ins.Append(diff.Text), del),
                Delete => (ins, del.Append(diff.Text)),
                _ => (ins, del)
            };

            if (diff.DiffOperation != Equal)
            {
                continue;
            }

            IEnumerable<Diff> consolidatedDiffsBeforeEqual = diff.DiffOperation switch
            {
                Equal when ins.Length > 0 && del.Length > 0 => Compute(del.ToString(), ins.ToString(), false, optimizeForSpeed, token),
                Equal when del.Length > 0 => Diff.Delete(del.ToString()).ItemAsEnumerable(),
                Equal when ins.Length > 0 => Diff.Insert(ins.ToString()).ItemAsEnumerable(),
                _ => []
            };

            foreach (Diff d in consolidatedDiffsBeforeEqual)
            {
                yield return d;
            }

            if (!diff.IsEmpty)
                yield return diff;

            ins.Clear();
            del.Clear();
        }
    }

    /// <summary>
    /// Find the 'middle snake' of a diff, split the problem in two
    /// and return the recursively constructed diff.
    /// See Myers 1986 paper: An O(ND) Difference Algorithm and Its Variations.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <param name="optimizeForSpeed"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    internal static IEnumerable<Diff> MyersDiffBisect(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2, bool optimizeForSpeed, CancellationToken token)
    {
        // Cache the text lengths to prevent multiple calls.
        int text1Length = text1.Length;
        int text2Length = text2.Length;
        int maxD = (text1Length + text2Length + 1) / 2;
        int vLength = 2 * maxD;
        int[] v1 = new int[vLength];
        int[] v2 = new int[vLength];
        for (int x = 0; x < vLength; x++)
        {
            v1[x] = -1;
        }
        for (int x = 0; x < vLength; x++)
        {
            v2[x] = -1;
        }
        v1[maxD + 1] = 0;
        v2[maxD + 1] = 0;
        int delta = text1Length - text2Length;
        // If the total number of characters is odd, then the front path will
        // collide with the reverse path.
        bool front = delta % 2 != 0;
        // Offsets for start and end of k loop.
        // Prevents mapping of space beyond the grid.
        int k1Start = 0;
        int k1End = 0;
        int k2Start = 0;
        int k2End = 0;
        for (int d = 0; d < maxD; d++)
        {
            // Bail out if cancelled.
            if (token.IsCancellationRequested)
            {
                break;
            }

            // Walk the front path one step.
            for (int k1 = -d + k1Start; k1 <= d - k1End; k1 += 2)
            {
                int k1Offset = maxD + k1;
                int x1;
                if (k1 == -d || k1 != d && v1[k1Offset - 1] < v1[k1Offset + 1])
                {
                    x1 = v1[k1Offset + 1];
                }
                else
                {
                    x1 = v1[k1Offset - 1] + 1;
                }
                int y1 = x1 - k1;
                while (x1 < text1Length && y1 < text2Length
                       && text1[x1] == text2[y1])
                {
                    x1++;
                    y1++;
                }
                v1[k1Offset] = x1;
                if (x1 > text1Length)
                {
                    // Ran off the right of the graph.
                    k1End += 2;
                }
                else if (y1 > text2Length)
                {
                    // Ran off the bottom of the graph.
                    k1Start += 2;
                }
                else if (front)
                {
                    int k2Offset = maxD + delta - k1;
                    if (k2Offset >= 0 && k2Offset < vLength && v2[k2Offset] != -1)
                    {
                        // Mirror x2 onto top-left coordinate system.
                        int x2 = text1Length - v2[k2Offset];
                        if (x1 >= x2)
                        {
                            // Overlap detected.
                            return BisectSplit(text1, text2, x1, y1, optimizeForSpeed, token);
                        }
                    }
                }
            }

            // Walk the reverse path one step.
            for (int k2 = -d + k2Start; k2 <= d - k2End; k2 += 2)
            {
                int k2Offset = maxD + k2;
                int x2;
                if (k2 == -d || k2 != d && v2[k2Offset - 1] < v2[k2Offset + 1])
                {
                    x2 = v2[k2Offset + 1];
                }
                else
                {
                    x2 = v2[k2Offset - 1] + 1;
                }
                int y2 = x2 - k2;
                while (x2 < text1Length && y2 < text2Length
                       && text1[text1Length - x2 - 1]
                       == text2[text2Length - y2 - 1])
                {
                    x2++;
                    y2++;
                }
                v2[k2Offset] = x2;
                if (x2 > text1Length)
                {
                    // Ran off the left of the graph.
                    k2End += 2;
                }
                else if (y2 > text2Length)
                {
                    // Ran off the top of the graph.
                    k2Start += 2;
                }
                else if (!front)
                {
                    int k1Offset = maxD + delta - k2;
                    if (k1Offset >= 0 && k1Offset < vLength && v1[k1Offset] != -1)
                    {
                        int x1 = v1[k1Offset];
                        int y1 = maxD + x1 - k1Offset;
                        // Mirror x2 onto top-left coordinate system.
                        x2 = text1Length - v2[k2Offset];
                        if (x1 >= x2)
                        {
                            // Overlap detected.
                            return BisectSplit(text1, text2, x1, y1, optimizeForSpeed, token);
                        }
                    }
                }
            }
        }
        // Diff took too long and hit the deadline or
        // number of Diffs equals number of characters, no commonality at all.
        return [Diff.Delete(text1), Diff.Insert(text2)];
    }

    /// <summary>
    /// Given the location of the 'middle snake', split the diff in two parts
    /// and recurse.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <param name="x">Index of split point in text1.</param>
    /// <param name="y">Index of split point in text2.</param>
    /// <param name="optimizeForSpeed"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static IEnumerable<Diff> BisectSplit(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2, int x, int y, bool optimizeForSpeed, CancellationToken token)
    {
        ReadOnlySpan<char> text1A = text1[..x];
        ReadOnlySpan<char> text2A = text2[..y];
        ReadOnlySpan<char> text1B = text1[x..];
        ReadOnlySpan<char> text2B = text2[y..];

        // Compute both Diffs serially.
        IEnumerable<Diff> diffsa = Compute(text1A, text2A, false, optimizeForSpeed, token);
        IEnumerable<Diff> diffsb = Compute(text1B, text2B, false, optimizeForSpeed, token);

        return diffsa.Concat(diffsb);
    }

}
