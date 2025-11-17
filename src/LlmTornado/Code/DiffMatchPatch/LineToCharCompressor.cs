using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LlmTornado.Code.DiffMatchPatch;

internal class LineToCharCompressor
{
    /// <summary>
    /// Compresses all lines of a text to a series of indexes (starting at \u0001, ending at (char)text.Length)
    /// </summary>
    /// <param name="text"></param>
    /// <param name="maxLines"></param>
    /// <returns></returns>
    public string Compress(ReadOnlySpan<char> text, int maxLines = char.MaxValue)
        => Encode(text, maxLines);

    private string Encode(ReadOnlySpan<char> text, int maxLines)
    {
        StringBuilder sb = new StringBuilder();
        int start = 0;
        int end = -1;
        while (end < text.Length - 1)
        {
            int i = text[start..].IndexOf('\n');
            end = _lineArray.Count == maxLines || i == -1 ? text.Length - 1 : i + start;
            string line = text[start..(end + 1)].ToString();
            EnsureHashed(line);
            sb.Append(this[line]);
            start = end + 1;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Decompresses a series of characters that was previously compressed back to the original lines of text.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string Decompress(string text)
        => text.Aggregate(new StringBuilder(), (sb, c) => sb.Append(this[(int)c])).Append(text.Length == char.MaxValue ? this[char.MaxValue] : "").ToString();

    // e.g. _lineArray[4] == "Hello\n"
    // e.g. _lineHash["Hello\n"] == 4
    private readonly List<string> _lineArray = [];
    private readonly Dictionary<string, char> _lineHash = new Dictionary<string, char>();

    private void EnsureHashed(string line)
    {
        if (_lineHash.ContainsKey(line)) return;
        _lineArray.Add(line);
        _lineHash.Add(line, (char)(_lineArray.Count - 1));
    }

    private char this[string line] => _lineHash[line];
    private string this[int c] => _lineArray[c];

}
