using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using LlmTornado.Agents.Dnd.Game;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyWorldState
{
    public FantasyAdventure Adventure { get; set; }
    public string SaveDataDirectory { get; set; } = "";
    public string AdventureRevisionId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public int CurrentActIndex { get; set; } = 0;
    public int CurrentSceneIndex { get; set; } = 0;
    public int CurrentSceneTurns { get; set; } = 0;
    public int CurrentTimeOfDay { get; set; } = 12; // Represented in 24-hour format
    public int RestCooldownHoursLeft { get; set; } = 12;
    public int CurrentDay { get; set; } = 1;
    public int HoursSinceLastRest { get; set; } = 0;
    public FantasyLocation CurrentLocation { get; set; }
    public bool GameCompleted { get; set; } = false;

    //Set with settings
    [JsonIgnore]
    public bool EnableTts { get { return _enableTts; } set { _enableTts = SetTtsEnabled(value); } }
    [JsonIgnore]
    private bool _enableTts { get; set; } = false;

    public FantasyDMResult LatestDmResultCache { get; set; } = new FantasyDMResult();

    [JsonIgnore]
    public FantasyScene? CurrentScene => Adventure.Acts.Length > CurrentActIndex && Adventure.Acts[CurrentActIndex].Scenes.Length > CurrentSceneIndex ? Adventure.Acts[CurrentActIndex].Scenes[CurrentSceneIndex] : null;

    [JsonIgnore]
    public FantasyAct? CurrentAct => Adventure.Acts.Length > CurrentActIndex ? Adventure.Acts[CurrentActIndex] : null;

    // Helper properties for standardized file paths
    [JsonIgnore]
    public string WorldStateFile => Path.Combine(SaveDataDirectory, "state.json");
    [JsonIgnore]
    public string MemoryFile => Path.Combine(SaveDataDirectory, "memory.md");
    [JsonIgnore]
    public string CompletedObjectivesFile => Path.Combine(SaveDataDirectory, "archive.md");

    [JsonIgnore]
    public string DmMemoryFile => Path.Combine(SaveDataDirectory, "dm_memory.md");
    [JsonIgnore]
    public string RecorderMemoryFile => Path.Combine(SaveDataDirectory, "recorder_memory.md");

    public bool SetTtsEnabled(bool enabled)
    {
        TTS_Controller.IsEnabled = enabled;
        SerializeToFile(WorldStateFile);
        return enabled;
    }

    /// <summary>
    /// Get Available routes from the current location.
    /// </summary>
    /// <returns></returns>
    public FantasyRoute[] GetAvailableRoutes()
    {
        return CurrentLocation.Routes;
    }

    /// <summary>
    /// Checks if the player can change location to the specified location ID or Name.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool CanChangeLocation(string id)
    {
        // Try to find location by ID first
        var route = GetAvailableRoutes().FirstOrDefault(r => r.ToLocationId == id);
        if (route != null)
        {
            return true;
        }
        // Try to find by Name in current scene locations
        var location = Adventure.Locations.FirstOrDefault(l => l.Name == id);
        if (location is not null)
        {
            var routeExists = GetAvailableRoutes().FirstOrDefault(r => r.ToLocationId == location.Id);
            if (routeExists is not null)
            {
                return true;
            }
        }
        return false;
    }


    /// <summary>
    /// Change the location of the player to the specified location ID or Name if on a connected route.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [Description("Attempts to change the player's location to the specified location ID or Name. Returns a status message indicating success or failure.")]
    public string MovePlayer([Description("ID or Name of location to change player to.")] string id)
    {
        if(!CanChangeLocation(id))
        {
            return $"Failed - Available Routes :\n {GetAvailableRoutesInfo()}";
        }

        // Try to find location by ID first
        FantasyLocation location;

        //Find in current scene locations first
        var route = GetAvailableRoutes().FirstOrDefault(r => r.ToLocationId == id);

        Console.WriteLine($"Attempting to change locations to {id}");

        if (route != null)
        {
            CurrentLocation = Adventure.Locations.FirstOrDefault(l=> l.Id == route.ToLocationId) ?? CurrentLocation;
            ProgressTime(route.DistanceInHours);
            Console.WriteLine(@$"You have changed location to {CurrentLocation.Name}.");
            return @$"Player has changed location to {CurrentLocation.Name}.";
        }

        // Try to find by Name in current scene locations
        location = Adventure.Locations.FirstOrDefault(l => l.Name == id);

        if (location is not null)
        {
            var routeExists = GetAvailableRoutes().First(r => r.ToLocationId == location.Id);
            if (routeExists is not null)
            {
                CurrentLocation = location;
                ProgressTime(routeExists.DistanceInHours);
                Console.WriteLine(@$"You have changed location to {CurrentLocation.Name}.");
                return @$"Player has changed location to {CurrentLocation.Name}.";
            }
            else
            {
                Console.WriteLine($"No route exists to location: {location.Name}");
            }
        }
        
        return $"Failed - Available Routes :\n {GetAvailableRoutesInfo()}";
    }

    /// <summary>
    /// Get a string summary of available routes from the current location.
    /// </summary>
    /// <returns></returns>
    public string GetAvailableRoutesInfo()
    {
        var routes = GetAvailableRoutes();
        string routeInfo = "";
        foreach (var rout in routes)
        {
            FantasyLocation l = Adventure.Locations.First(l => l.Id == rout.ToLocationId);
            if (l== null) continue;
            routeInfo += $"- {l.Name} = Move Time in hours: [{rout.DistanceInHours}] Route Description:{rout.Description}\n";
        }
        return routeInfo ;
    }

    /// <summary>
    /// Progress the in-game time by the specified number of hours.
    /// </summary>
    /// <param name="hours"></param>
    public void ProgressTime(int hours)
    {
        CurrentTimeOfDay += hours;
        RestCooldownHoursLeft = Math.Max(0, RestCooldownHoursLeft - hours);
        HoursSinceLastRest += hours;

        if (CurrentTimeOfDay >= 24)
        {
            CurrentTimeOfDay = CurrentTimeOfDay % 24;
            CurrentDay++;
        }

        SerializeToFile(WorldStateFile);
    }

    /// <summary>
    /// Rest for 8 hours if possible, resetting rest cooldown and updating time.
    /// </summary>
    /// <returns></returns>
    public bool Rest()
    {
        if(RestCooldownHoursLeft > 0)
        {
            Console.WriteLine("Cannot rest yet. Cooldown hours left: " + RestCooldownHoursLeft);
            return false;
        }

        if(CurrentLocation.CanRestHere == false)
        {
            Console.WriteLine("Cannot rest at current location: " + CurrentLocation.Name);
            return false;
        }

        RestCooldownHoursLeft = 8;
        HoursSinceLastRest = 0;
        ProgressTime(8); // Assume player rest 8 hrs
        SerializeToFile(WorldStateFile);

        return true;
    }

    /// <summary>
    /// Save this world state to a JSON file.
    /// </summary>
    /// <param name="filePath"></param>
    public void SerializeToFile(string filePath)
    {
        LastSaved = DateTime.UtcNow;
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = false,
            AllowTrailingCommas = true,
            IncludeFields = false,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
        });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Load the world state from a JSON file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static FantasyWorldState DeserializeFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);

        try
        {
            // Try to deserialize with strict settings first
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                AllowTrailingCommas = true,
                IncludeFields = false,
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
            };
            return System.Text.Json.JsonSerializer.Deserialize<FantasyWorldState>(json, options) ?? new FantasyWorldState();
        }
        catch (System.Text.Json.JsonException jex)
        {
            // Fallback to more lenient deserialization
            Console.WriteLine("Warning: Deserialization failed with strict settings. Falling back to lenient deserialization.");
            Console.WriteLine($"Details: {jex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error during deserialization: " + ex.Message);
        }

        return System.Text.Json.JsonSerializer.Deserialize<FantasyWorldState>(json) ?? new FantasyWorldState();
    }

    public void MoveToNextScene()
    {
        var currentAct = Adventure.Acts[CurrentActIndex];
        CurrentSceneTurns = 0;
        if (CurrentSceneIndex + 1 < currentAct.Scenes.Count())
        {
            CurrentSceneIndex++;
        }
        else if (CurrentActIndex + 1 < Adventure.Acts.Count())
        {
            CurrentActIndex++;
            CurrentSceneIndex = 0;
        }
        else
        {
            CurrentActIndex = 0;
            CurrentSceneIndex = 0;

            GameCompleted = true;
        }
        SerializeToFile(WorldStateFile);
    }


    public string GetNextScene()
    {
        var currentAct = Adventure.Acts[CurrentActIndex];
        if (CurrentSceneIndex + 1 < currentAct.Scenes.Count())
        {
            return currentAct.Scenes[CurrentSceneIndex + 1].ToString();
        }
        else if (CurrentActIndex + 1 < Adventure.Acts.Count())
        {
            return Adventure.Acts[CurrentActIndex + 1].Scenes[0].ToString();
        }
        else
        {
            return "End of Adventure";
        }
    }
}

