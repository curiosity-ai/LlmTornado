using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Persistence;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Initializes the game world with locations, items, and starting state
/// </summary>
public class GameWorldInitializer
{
    public static GameState CreateNewGame(string? adventureId = null)
    {
        var gameState = new GameState
        {
            CurrentLocationName = "Tavern",
            CurrentAdventureId = adventureId
        };

        if (!string.IsNullOrEmpty(adventureId))
        {
            var adventurePersistence = new AdventurePersistence();
            var adventure = adventurePersistence.LoadAdventure(adventureId);
            
            if (adventure != null)
            {
                InitializeFromAdventure(adventure, gameState);
                return gameState;
            }
        }

        throw new InvalidOperationException("Adventure ID is null or adventure could not be loaded.");
    }

    /// <summary>
    /// Initializes game world from a generated Adventure
    /// </summary>
    internal static void InitializeFromAdventure(Adventure adventure, GameState gameState)
    {
        var locations = new Dictionary<string, Location>();

        // Convert all scenes to locations
        foreach (var sceneKvp in adventure.Scenes)
        {
            var location = ConvertSceneToLocation(sceneKvp.Value, adventure.Scenes);
            locations[location.Name] = location;
        }

        gameState.Locations = locations;

        // Determine starting location
        string startingLocationName = ""; // Default fallback
        
        if (adventure.MainQuestLine.Count > 0 && !string.IsNullOrEmpty(adventure.MainQuestLine[0].StartSceneId))
        {
            var startSceneId = adventure.MainQuestLine[0].StartSceneId[1].ToString();
            if (adventure.Scenes.TryGetValue(startSceneId, out var startScene))
            {
                startingLocationName = startScene.Name;
            }
        }
        else if (adventure.Scenes.Count > 0)
        {
            // Use first scene as starting location
            startingLocationName = adventure.Scenes.First().Value.Name;
        }

        gameState.CurrentLocationName = startingLocationName;
        gameState.CurrentAdventureId = adventure.Id;

        if(gameState.CurrentLocationName == "")
        {
            throw new InvalidOperationException("Could not determine starting location from adventure.");
        }
    }

    /// <summary>
    /// Converts an Adventure Scene to a GameState Location
    /// </summary>
    private static Location ConvertSceneToLocation(Scene scene, Dictionary<string, Scene> allScenes)
    {
        var location = new Location
        {
            Name = scene.Name,
            Description = scene.Description,
            NPCs = new List<string>(scene.NPCs),
            Items = new List<Item>()
        };

        // Convert Scene.Items (List<string>) to Location.Items (List<Item>)
        foreach (var itemName in scene.Items)
        {
            var item = CreateItemFromName(itemName);
            if (item != null)
            {
                location.Items.Add(item);
            }
        }

        // Convert Scene.Exits (Exit[] with SceneIds) to Location.Exits (List<string> with Scene Names)
        var exitNames = new List<string>();
        foreach (var exit in scene.Exits)
        {
            if (allScenes.TryGetValue(exit.SceneId, out var targetScene))
            {
                // Use the exit name if provided, otherwise use the target scene name
                string exitName = !string.IsNullOrEmpty(exit.Name) ? exit.Name : targetScene.Name;
                exitNames.Add(exitName);
            }
        }
        location.Exits = exitNames;

        return location;
    }

    /// <summary>
    /// Creates a basic Item object from a string name
    /// </summary>
    private static Item? CreateItemFromName(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return null;
        }

        // Create a basic item with default properties
        // In a more sophisticated implementation, this could look up item definitions
        var item = new Item
        {
            Name = itemName,
            Description = $"A {itemName.ToLower()}",
            Type = DetermineItemType(itemName),
            Value = 10, // Default value
            Properties = new Dictionary<string, int>()
        };

        // Add basic properties based on item type
        if (item.Type == "weapon")
        {
            item.Properties["damage"] = 10;
        }
        else if (item.Type == "armor")
        {
            item.Properties["defense"] = 5;
        }
        else if (item.Type == "consumable")
        {
            item.Properties["healing"] = 25;
        }

        return item;
    }

    /// <summary>
    /// Determines item type from name (simple heuristic)
    /// </summary>
    private static string DetermineItemType(string itemName)
    {
        var lowerName = itemName.ToLower();
        
        if (lowerName.Contains("sword") || lowerName.Contains("axe") || lowerName.Contains("dagger") || 
            lowerName.Contains("staff") || lowerName.Contains("bow") || lowerName.Contains("weapon"))
        {
            return "weapon";
        }
        
        if (lowerName.Contains("armor") || lowerName.Contains("shield") || lowerName.Contains("helmet") ||
            lowerName.Contains("plate") || lowerName.Contains("mail"))
        {
            return "armor";
        }
        
        if (lowerName.Contains("potion") || lowerName.Contains("scroll") || lowerName.Contains("food") ||
            lowerName.Contains("ration") || lowerName.Contains("consumable"))
        {
            return "consumable";
        }
        
        if (lowerName.Contains("key") || lowerName.Contains("map") || lowerName.Contains("quest") ||
            lowerName.Contains("amulet") || lowerName.Contains("artifact"))
        {
            return "quest";
        }

        return "misc";
    }

    public static PlayerCharacter CreatePlayerCharacter(string name, CharacterClass characterClass, CharacterRace race)
    {
        var player = new PlayerCharacter
        {
            Name = name,
            Class = characterClass,
            Race = race,
            Level = 1,
            Health = 100,
            MaxHealth = 100,
            Experience = 0,
            Gold = 50,
            IsAI = false
        };

        // Apply class modifiers
        var classDefinition = CharacterClassFactory.GetClassDefinition(characterClass);
        classDefinition.ApplyToCharacter(player);

        // Apply race bonuses
        var raceDefinition = CharacterRaceFactory.GetRaceDefinition(race);
        raceDefinition.ApplyToCharacter(player);

        // Add common starting items
        player.Inventory.Add("Health Potion");
        player.Inventory.Add("Rations");

        return player;
    }

    public static PlayerCharacter CreateAIPlayer(string name)
    {
        var random = new Random();
        var classes = Enum.GetValues<CharacterClass>();
        var races = Enum.GetValues<CharacterRace>();

        var selectedClass = classes[random.Next(classes.Length)];
        var selectedRace = races[random.Next(races.Length)];

        var player = CreatePlayerCharacter(name, selectedClass, selectedRace);
        player.IsAI = true;

        return player;
    }
}
