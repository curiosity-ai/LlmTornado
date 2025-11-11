using System.Text.Json;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Game;

namespace LlmTornado.Agents.Dnd.Persistence;

/// <summary>
/// Handles saving and loading game state
/// </summary>
internal class FantasyWorldPersistence
{
    private readonly string _savePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly FantasyAdventurePersistence _adventurePersistence;

    public FantasyWorldPersistence(string? customSavePath = null)
    {
        _savePath = customSavePath ?? Path.Combine(
            Directory.GetCurrentDirectory(),
            "saves"
        );

        try
        {
            if (!Directory.Exists(_savePath))
                Directory.CreateDirectory(_savePath);
        }
        catch (Exception ex)
        {
            //
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        _adventurePersistence = new FantasyAdventurePersistence();
    }

    /// <summary>
    /// Saves the current game state to a file
    /// </summary>
    public async Task<string> SaveGameAsync(FantasyWorldState gameState)
    {
        gameState.LastSaved = DateTime.UtcNow;
        string fileName = $"save_{gameState.AdventureTitle.Replace(" ", "_")}.json";
        string fullPath = Path.Combine(_savePath, fileName);

        string json = JsonSerializer.Serialize(gameState, _jsonOptions);
        await File.WriteAllTextAsync(fullPath, json);

        return fullPath;
    }

    /// <summary>
    /// Loads a game state from a file
    /// </summary>
    public async Task<FantasyWorldState?> LoadGameAsync(FantasyWorldState gameState)
    {
        string fileName = $"save_{gameState.AdventureTitle.Replace(" ", "_")}.json";
        string fullPath = Path.Combine(_savePath, fileName);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(fullPath);
        gameState = JsonSerializer.Deserialize<FantasyWorldState>(json, _jsonOptions);

        return gameState;
    }
}
