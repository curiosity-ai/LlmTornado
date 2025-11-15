using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using System.ComponentModel;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyWorldState
{
    public FantasyAdventure Adventure;
    public FantasyAdventureResult AdventureResult;
    public string SaveDataDirectory { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public string CurrentLocationName => CurrentLocation.Name ?? "Unknown";
    public int CurrentAct { get; set; } = 0;
    public int CurrentScene { get; set; } = 0;
    public int CurrentSceneTurns { get; set; } = 0;
    public FantasyLocation CurrentLocation { get; set; }
    public bool GameCompleted { get; set; } = false;

    // Helper properties for standardized file paths
    public string WorldStateFile => Path.Combine(SaveDataDirectory, "state.json");
    public string MemoryFile => Path.Combine(SaveDataDirectory, "memory.md");
    public string CompletedObjectivesFile => Path.Combine(SaveDataDirectory, "archive.md");
    public string AdventureFile => Path.Combine(SaveDataDirectory, "adventure.json");

    [Description("Changes the current location to the location with the specified ID or Name.")]
    public string ChangeLocation([Description("The ID or Name of the location to change to.")] string id)
    {
        // Try to find location by ID first
        FantasyLocation location;

        //Find in current scene locations first
        location = Adventure.Acts[CurrentAct].Scenes[CurrentScene].Locations.FirstOrDefault(l => l.Id == id);
        Console.WriteLine($"Attempting to change locations to {id}");
        if (location != null)
        {
            CurrentLocation = location;
            SerializeToFile(WorldStateFile);
            Console.WriteLine(@$"You have changed location to {CurrentLocation.Name}.");
            return @$"You have changed location to {CurrentLocation.Name}.";
        }

        // Try to find by Name in current scene locations
        location = Adventure.Acts[CurrentAct].Scenes[CurrentScene].Locations.FirstOrDefault(l => l.Name == id);

        if (location is not null)
        {
            CurrentLocation = location;
            SerializeToFile(WorldStateFile);
            Console.WriteLine(@$"You have changed location to {CurrentLocation.Name}.");
            return @$"You have changed location to {CurrentLocation.Name}.";
        }

        // If not found in current scene, try to find by ID in all locations
        location = Adventure.Locations.FirstOrDefault(l => l.Id == id);

        if (location is not null)
        {
            return @$"You Cannot changed location to {CurrentLocation.Name} from this scene.";
        }

        // If not found by ID, try to find by Name
        location = Adventure.Locations.FirstOrDefault(location => location.Name == id);

        if (location is not null)
        {
            // Location not found
            return @$"You Cannot changed location to {CurrentLocation.Name} from this scene.";
        }

        return @$"Unknown location.. Player not moved";
    }

    public void SerializeToFile(string filePath)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(filePath, json);
    }

    public static FantasyWorldState DeserializeFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return System.Text.Json.JsonSerializer.Deserialize<FantasyWorldState>(json) ?? new FantasyWorldState();
    }

    public void MoveToNextScene()
    {
        var currentAct = Adventure.Acts[CurrentAct];
        CurrentSceneTurns = 0;
        if (CurrentScene + 1 < currentAct.Scenes.Count())
        {
            CurrentScene++;
        }
        else if (CurrentAct + 1 < Adventure.Acts.Count())
        {
            CurrentAct++;
            CurrentScene = 0;
        }
        else
        {
            CurrentAct = 0;
            CurrentScene = 0;

            GameCompleted = true;
        }
        SerializeToFile(WorldStateFile);
    }
}

