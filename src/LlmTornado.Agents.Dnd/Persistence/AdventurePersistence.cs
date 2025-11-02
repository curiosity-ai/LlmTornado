using System.Text.Json;
using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.Persistence;

/// <summary>
/// Handles saving and loading of generated adventures
/// </summary>
public class AdventurePersistence
{
    private readonly string _savePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public AdventurePersistence(string? customSavePath = null)
    {
        _savePath = customSavePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LlmTornado.Dnd",
            "adventures"
        );

        Directory.CreateDirectory(_savePath);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// Save an adventure to disk
    /// </summary>
    public void SaveAdventure(Adventure adventure)
    {
        var fileName = $"{adventure.Id}.json";
        var filePath = Path.Combine(_savePath, fileName);
        
        var json = JsonSerializer.Serialize(adventure, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Load an adventure by ID
    /// </summary>
    public Adventure? LoadAdventure(string adventureId)
    {
        var fileName = $"{adventureId}.json";
        var filePath = Path.Combine(_savePath, fileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<Adventure>(json, _jsonOptions);
    }

    /// <summary>
    /// List all available adventures
    /// </summary>
    public List<AdventureInfo> ListAdventures()
    {
        var adventures = new List<AdventureInfo>();
        var files = Directory.GetFiles(_savePath, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var adventure = JsonSerializer.Deserialize<Adventure>(json, _jsonOptions);
                
                if (adventure != null)
                {
                    adventures.Add(new AdventureInfo
                    {
                        Id = adventure.Id,
                        Name = adventure.Name,
                        Description = adventure.Description,
                        Difficulty = adventure.Difficulty,
                        GeneratedAt = adventure.GeneratedAt,
                        QuestCount = adventure.MainQuestLine.Count,
                        Progress = $"{adventure.CompletedQuestIds.Count}/{adventure.MainQuestLine.Count} quests"
                    });
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        return adventures.OrderByDescending(a => a.GeneratedAt).ToList();
    }

    /// <summary>
    /// Delete an adventure
    /// </summary>
    public bool DeleteAdventure(string adventureId)
    {
        var fileName = $"{adventureId}.json";
        var filePath = Path.Combine(_savePath, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Summary information about an adventure
/// </summary>
public class AdventureInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DifficultyLevel Difficulty { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int QuestCount { get; set; }
    public string Progress { get; set; } = string.Empty;
}
