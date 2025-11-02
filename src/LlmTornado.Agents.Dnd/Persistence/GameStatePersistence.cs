using System.Text.Json;
using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.Persistence;

/// <summary>
/// Handles saving and loading game state
/// </summary>
public class GameStatePersistence
{
    private readonly string _savePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public GameStatePersistence(string? customSavePath = null)
    {
        _savePath = customSavePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LlmTornado.Dnd",
            "saves"
        );

        Directory.CreateDirectory(_savePath);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Saves the current game state to a file
    /// </summary>
    public async Task<string> SaveGameAsync(GameState gameState)
    {
        gameState.LastSaved = DateTime.UtcNow;
        string fileName = $"save_{gameState.SessionId}.json";
        string fullPath = Path.Combine(_savePath, fileName);

        string json = JsonSerializer.Serialize(gameState, _jsonOptions);
        await File.WriteAllTextAsync(fullPath, json);

        return fullPath;
    }

    /// <summary>
    /// Loads a game state from a file
    /// </summary>
    public async Task<GameState?> LoadGameAsync(string sessionId)
    {
        string fileName = $"save_{sessionId}.json";
        string fullPath = Path.Combine(_savePath, fileName);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(fullPath);
        return JsonSerializer.Deserialize<GameState>(json, _jsonOptions);
    }

    /// <summary>
    /// Lists all available save files
    /// </summary>
    public List<(string SessionId, DateTime LastSaved)> ListSaves()
    {
        var saves = new List<(string, DateTime)>();

        if (!Directory.Exists(_savePath))
        {
            return saves;
        }

        foreach (var file in Directory.GetFiles(_savePath, "save_*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                var gameState = JsonSerializer.Deserialize<GameState>(json, _jsonOptions);
                if (gameState != null)
                {
                    saves.Add((gameState.SessionId, gameState.LastSaved));
                }
            }
            catch
            {
                // Skip corrupted files
            }
        }

        return saves.OrderByDescending(s => s.Item2).ToList();
    }

    /// <summary>
    /// Deletes a save file
    /// </summary>
    public bool DeleteSave(string sessionId)
    {
        string fileName = $"save_{sessionId}.json";
        string fullPath = Path.Combine(_savePath, fileName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return true;
        }

        return false;
    }
}
