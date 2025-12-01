using System.IO;
using System.Text;

namespace LlmTornado.Agents.Dnd.Utility;

internal static class FileNameHelpers
{
    public static string ToSafeFolderName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "untitled";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value.Trim())
        {
            if (invalidChars.Contains(character) || char.IsControl(character))
            {
                builder.Append('_');
                continue;
            }

            builder.Append(char.IsWhiteSpace(character) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim('_');
        return string.IsNullOrEmpty(sanitized) ? "untitled" : sanitized;
    }
}

