using System.Text;

namespace LlmTornado.Mcp;

/// <summary>
/// Helper class for interacting with the meme MCP server.
/// Provides utilities for building meme URLs and encoding text according to memegen API spec.
/// </summary>
public static class McpMemeCls
{
    private static readonly Dictionary<char, string> charReplacements = new Dictionary<char, string>
    {
        { '?', "~q" },
        { '&', "~a" },
        { '%', "~p" },
        { '#', "~h" },
        { '/', "~s" },
        { '\\', "~b" },
        { '<', "~l" },
        { '>', "~g" },
        { '\n', "~n" },
        { ' ', "_" }
    };

    /// <summary>
    /// Transforms text for memegen API URL encoding using a single-pass algorithm.
    /// Handles double characters, spaces, and reserved URL characters according to memegen API spec.
    /// </summary>
    /// <param name="text">The text to transform</param>
    /// <returns>Transformed text ready for memegen URL</returns>
    public static string TransformTextForUrl(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        StringBuilder result = new StringBuilder(text.Length * 2);
        int i = 0;

        while (i < text.Length)
        {
            char current = text[i];
            bool handled = false;

            // Handle double characters first (preserve them, don't replace)
            if (i < text.Length - 1)
            {
                char next = text[i + 1];

                if ((current == '_' && next == '_') ||
                    (current == '-' && next == '-') ||
                    (current == '"' && next == '"') ||
                    (current == '\'' && next == '\''))
                {
                    // Preserve double characters as-is (or convert "" to '')
                    if (current == '"' && next == '"')
                    {
                        result.Append("''");
                    }
                    else
                    {
                        result.Append(current);
                        result.Append(next);
                    }
                    i += 2;
                    handled = true;
                }
            }

            if (!handled)
            {
                // Handle single character replacements
                if (charReplacements.TryGetValue(current, out string? replacement))
                {
                    result.Append(replacement);
                }
                else if (current == '"')
                {
                    // Convert single double quote to single quote
                    result.Append('\'');
                }
                else
                {
                    result.Append(current);
                }
                i++;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Builds a meme URL from template ID and text lines.
    /// </summary>
    /// <param name="templateId">The meme template ID</param>
    /// <param name="textLines">Array of text lines for the meme</param>
    /// <param name="baseUrl">Base URL for the memegen server (default: http://localhost:5000)</param>
    /// <returns>Complete meme URL</returns>
    public static string BuildMemeUrl(string templateId, string[] textLines, string baseUrl = "http://localhost:5000")
    {
        if (string.IsNullOrEmpty(templateId))
            throw new ArgumentException("Template ID cannot be null or empty", nameof(templateId));

        if (textLines == null || textLines.Length == 0)
            throw new ArgumentException("Text lines cannot be null or empty", nameof(textLines));

        string[] transformed = new string[textLines.Length];
        for (int i = 0; i < textLines.Length; i++)
        {
            transformed[i] = TransformTextForUrl(textLines[i]);
        }

        return $"{baseUrl}/images/{templateId}/{string.Join("/", transformed)}.png";
    }

    /// <summary>
    /// Builds a placeholder preview URL for a meme template.
    /// </summary>
    /// <param name="templateId">The meme template ID</param>
    /// <param name="lineCount">Number of text lines the template expects</param>
    /// <param name="placeholder">A prototype for line placeholder, {0} = index of line.</param>
    /// <param name="baseUrl">Base URL for the memegen server (default: http://localhost:5000)</param>
    /// <returns>Placeholder preview URL</returns>
    public static string BuildPreviewUrl(string templateId, int lineCount, string placeholder = "TEXT_{0}", string baseUrl = "http://localhost:5000")
    {
        if (string.IsNullOrEmpty(templateId))
            throw new ArgumentException("Template ID cannot be null or empty", nameof(templateId));

        if (lineCount <= 0)
            throw new ArgumentException("Line count must be greater than 0", nameof(lineCount));

        string[] placeholders = new string[lineCount];
        for (int i = 0; i < lineCount; i++)
        {
            placeholders[i] = placeholder.Replace("{0}", $"{i + 1}");
        }

        return $"{baseUrl}/images/{templateId}/{string.Join("/", placeholders)}.png";
    }
}

