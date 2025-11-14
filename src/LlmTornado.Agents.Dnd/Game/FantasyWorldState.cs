using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyWorldState
{
    public FantasyAdventure Adventure { get; set; }
    public FantasyAdventureResult AdventureResult { get; set; }
    public string AdventureTitle { get; set; } = "";
    public string AdventureFile { get; set; } = "";
    public string WorldStateFile { get; set; } = "";
    public string CompletedObjectivesFile { get; set; } = "";
    public string MemoryFile { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public string CurrentLocationName { get; set; } = "Unknown";
    public int CurrentAct { get; set; } = 0;
    public int CurrentScene { get; set; } = 0;

    public int CurrentSceneTurns = 0;

    public bool GameCompleted { get; set; } = false;

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
        var currentAct = AdventureResult.Acts[CurrentAct];
        CurrentSceneTurns = 0;
        if (CurrentScene + 1 < currentAct.Scenes.Count())
        {
            CurrentScene++;
        }
        else if (CurrentAct + 1 < AdventureResult.Acts.Count())
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

