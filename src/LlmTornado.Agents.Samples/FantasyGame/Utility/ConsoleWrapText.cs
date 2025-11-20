using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.Utility;

public class ConsoleWrapText
{
    public static void WriteLines(string text, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string[] words = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder currentLine = new StringBuilder();

        foreach (string word in words)
        {
            // If adding this word would exceed maxWidth
            if (currentLine.Length > 0 && currentLine.Length + 1 + word.Length > maxWidth)
            {
                // Output current line and start fresh
                Console.WriteLine(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(word);
            }
            else
            {
                // Add word to current line
                if (currentLine.Length > 0)
                {
                    currentLine.Append(" ");
                }
                currentLine.Append(word);
            }
        }

        // Output any remaining text
        if (currentLine.Length > 0)
        {
            Console.WriteLine(currentLine.ToString());
        }
    }
}
