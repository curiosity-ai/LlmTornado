using System.Text.Json;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Game;

namespace LlmTornado.Agents.Dnd.Persistence;

/// <summary>
/// Handles saving and loading game state
/// </summary>
public class FantasyWorldPersistence
{
    private readonly string _savePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly AdventurePersistence _adventurePersistence;

    public FantasyWorldPersistence(string? customSavePath = null)
    {
        _savePath = customSavePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LlmTornado.Dnd.Fantasy",
            "saves"
        );

        Directory.CreateDirectory(_savePath);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        _adventurePersistence = new AdventurePersistence();
    }

    /// <summary>
    /// Saves the current game state to a file
    /// </summary>
    public async Task<string> SaveGameAsync(FantasyWorldState gameState)
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
        var gameState = JsonSerializer.Deserialize<GameState>(json, _jsonOptions);
        
        if (gameState != null)
        {
            // Ensure Adventure locations are loaded if Adventure ID is set
            await EnsureAdventureLocationsLoadedAsync(gameState);
        }

        return gameState;
    }

    /// <summary>
    /// Ensures that Locations are loaded from Adventure if CurrentAdventureId is set
    /// </summary>
    private async Task EnsureAdventureLocationsLoadedAsync(GameState gameState)
    {
        if (string.IsNullOrEmpty(gameState.CurrentAdventureId))
        {
            return;
        }

        var adventure = _adventurePersistence.LoadAdventure(gameState.CurrentAdventureId);
        if (adventure != null)
        {
            // Re-initialize locations from Adventure
            GameWorldInitializer.InitializeFromAdventure(adventure, gameState);
        }
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
