using System.IO;
using System.Text.Json;

namespace LlmTornado.Agents.Dnd.Game;

internal class UserSettings
{
    public bool EnableTts { get; set; } = true;

    public static UserSettings Load(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new UserSettings();
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }
}

