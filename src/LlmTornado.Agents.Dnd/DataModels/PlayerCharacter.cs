namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents a player character in the game
/// </summary>
public class PlayerCharacter
{
    public string Name { get; set; } = string.Empty;
    public CharacterClass Class { get; set; } = CharacterClass.Warrior;
    public CharacterRace Race { get; set; } = CharacterRace.Human;
    public int Level { get; set; } = 1;
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public int Experience { get; set; } = 0;
    public int Gold { get; set; } = 50;
    public Dictionary<string, int> Stats { get; set; } = new();
    public List<string> Inventory { get; set; } = new();
    public List<string> Abilities { get; set; } = new();
    public bool IsAI { get; set; } = false;

    public PlayerCharacter()
    {
        Stats = new Dictionary<string, int>
        {
            { "Strength", 10 },
            { "Dexterity", 10 },
            { "Constitution", 10 },
            { "Intelligence", 10 },
            { "Wisdom", 10 },
            { "Charisma", 10 }
        };
    }
}
