using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Initializes the game world with locations, items, and starting state
/// </summary>
public class GameWorldInitializer
{
    public static GameState CreateNewGame()
    {
        var gameState = new GameState
        {
            CurrentLocationName = "Tavern"
        };

        InitializeLocations(gameState);
        return gameState;
    }

    private static void InitializeLocations(GameState gameState)
    {
        var locations = new Dictionary<string, Location>
        {
            ["Tavern"] = new Location
            {
                Name = "Tavern",
                Description = "A warm, bustling tavern filled with adventurers. The smell of roasted meat and ale fills the air. A mysterious hooded figure sits in the corner.",
                Exits = new List<string> { "Town Square", "Inn Rooms" },
                NPCs = new List<string> { "Bartender", "Mysterious Stranger" },
                Items = new List<Item>
                {
                    new Item { Name = "Health Potion", Description = "Restores 50 health", Type = "consumable", Value = 25, Properties = new Dictionary<string, int> { { "healing", 50 } } }
                }
            },
            ["Town Square"] = new Location
            {
                Name = "Town Square",
                Description = "The heart of the town, with a grand fountain at its center. Merchants hawk their wares, and town criers announce the latest news.",
                Exits = new List<string> { "Tavern", "Market", "City Gates", "Temple" },
                NPCs = new List<string> { "Town Guard", "Merchant" },
                Items = new List<Item>()
            },
            ["Market"] = new Location
            {
                Name = "Market",
                Description = "A busy marketplace with stalls selling weapons, armor, and supplies. The sound of haggling fills the air.",
                Exits = new List<string> { "Town Square" },
                NPCs = new List<string> { "Weapon Smith", "Armor Dealer" },
                Items = new List<Item>
                {
                    new Item { Name = "Iron Sword", Description = "A well-crafted iron sword", Type = "weapon", Value = 50, Properties = new Dictionary<string, int> { { "damage", 15 } } },
                    new Item { Name = "Leather Armor", Description = "Basic leather armor", Type = "armor", Value = 40, Properties = new Dictionary<string, int> { { "defense", 10 } } }
                }
            },
            ["City Gates"] = new Location
            {
                Name = "City Gates",
                Description = "The massive gates of the city. Beyond them lies the wilderness, full of danger and adventure.",
                Exits = new List<string> { "Town Square", "Forest Path" },
                NPCs = new List<string> { "Gate Guard" },
                Items = new List<Item>()
            },
            ["Forest Path"] = new Location
            {
                Name = "Forest Path",
                Description = "A winding path through dense forest. Strange sounds echo from deeper in the woods.",
                Exits = new List<string> { "City Gates", "Dark Forest", "Abandoned Camp" },
                NPCs = new List<string>(),
                Items = new List<Item>
                {
                    new Item { Name = "Wooden Staff", Description = "A simple wooden walking staff", Type = "weapon", Value = 10, Properties = new Dictionary<string, int> { { "damage", 8 } } }
                }
            },
            ["Dark Forest"] = new Location
            {
                Name = "Dark Forest",
                Description = "The forest grows darker and more ominous here. You hear growling in the distance.",
                Exits = new List<string> { "Forest Path", "Cave Entrance" },
                NPCs = new List<string> { "Goblin Scout" },
                Items = new List<Item>()
            },
            ["Cave Entrance"] = new Location
            {
                Name = "Cave Entrance",
                Description = "A dark cave entrance. Ancient runes are carved into the stone. A sense of dread washes over you.",
                Exits = new List<string> { "Dark Forest", "Cave Interior" },
                NPCs = new List<string>(),
                Items = new List<Item>
                {
                    new Item { Name = "Torch", Description = "Provides light in dark places", Type = "consumable", Value = 5, Properties = new Dictionary<string, int> { { "light", 100 } } }
                }
            },
            ["Cave Interior"] = new Location
            {
                Name = "Cave Interior",
                Description = "Deep within the cave, you find a chamber filled with treasure... and danger.",
                Exits = new List<string> { "Cave Entrance" },
                NPCs = new List<string> { "Cave Troll" },
                Items = new List<Item>
                {
                    new Item { Name = "Golden Amulet", Description = "An ancient amulet radiating power", Type = "quest", Value = 500, Properties = new Dictionary<string, int> { { "magic", 25 } } }
                }
            },
            ["Temple"] = new Location
            {
                Name = "Temple",
                Description = "A peaceful temple dedicated to the gods. Healers tend to the wounded here.",
                Exits = new List<string> { "Town Square" },
                NPCs = new List<string> { "High Priest", "Healer" },
                Items = new List<Item>()
            },
            ["Abandoned Camp"] = new Location
            {
                Name = "Abandoned Camp",
                Description = "An old campsite, long abandoned. Scattered supplies and a cold fire pit remain.",
                Exits = new List<string> { "Forest Path" },
                NPCs = new List<string>(),
                Items = new List<Item>
                {
                    new Item { Name = "Old Map", Description = "A worn map showing nearby locations", Type = "quest", Value = 15, Properties = new Dictionary<string, int>() }
                }
            },
            ["Inn Rooms"] = new Location
            {
                Name = "Inn Rooms",
                Description = "Comfortable rooms for rent. A good place to rest and recover.",
                Exits = new List<string> { "Tavern" },
                NPCs = new List<string> { "Innkeeper" },
                Items = new List<Item>()
            }
        };

        gameState.Locations = locations;
    }

    public static PlayerCharacter CreatePlayerCharacter(string name, string characterClass, string race)
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

        // Adjust stats based on class
        switch (characterClass.ToLower())
        {
            case "warrior":
                player.Stats["Strength"] = 15;
                player.Stats["Constitution"] = 14;
                player.Abilities.Add("Power Strike");
                player.Inventory.Add("Iron Sword");
                break;
            case "mage":
                player.Stats["Intelligence"] = 16;
                player.Stats["Wisdom"] = 13;
                player.Abilities.Add("Fireball");
                player.Inventory.Add("Wooden Staff");
                break;
            case "rogue":
                player.Stats["Dexterity"] = 16;
                player.Stats["Charisma"] = 13;
                player.Abilities.Add("Sneak Attack");
                player.Inventory.Add("Dagger");
                break;
            case "cleric":
                player.Stats["Wisdom"] = 15;
                player.Stats["Constitution"] = 13;
                player.Abilities.Add("Heal");
                player.Inventory.Add("Mace");
                break;
        }

        // Adjust stats based on race
        switch (race.ToLower())
        {
            case "human":
                player.Stats["Charisma"] += 2;
                break;
            case "elf":
                player.Stats["Dexterity"] += 2;
                player.Stats["Intelligence"] += 1;
                break;
            case "dwarf":
                player.Stats["Constitution"] += 2;
                player.Stats["Strength"] += 1;
                break;
            case "halfling":
                player.Stats["Dexterity"] += 2;
                player.Stats["Charisma"] += 1;
                break;
        }

        player.Inventory.Add("Health Potion");
        player.Inventory.Add("Rations");

        return player;
    }

    public static PlayerCharacter CreateAIPlayer(string name)
    {
        var random = new Random();
        string[] classes = { "Warrior", "Mage", "Rogue", "Cleric" };
        string[] races = { "Human", "Elf", "Dwarf", "Halfling" };

        string selectedClass = classes[random.Next(classes.Length)];
        string selectedRace = races[random.Next(races.Length)];

        var player = CreatePlayerCharacter(name, selectedClass, selectedRace);
        player.IsAI = true;

        return player;
    }
}
