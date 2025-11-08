namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Base class for character race definitions
/// </summary>
public abstract class CharacterRaceDefinition
{
    public abstract CharacterRace RaceType { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Dictionary<string, int> StatBonuses { get; }

    /// <summary>
    /// Apply this race's bonuses to a character's stats
    /// </summary>
    public void ApplyToCharacter(PlayerCharacter character)
    {
        foreach (var bonus in StatBonuses)
        {
            if (character.Stats.ContainsKey(bonus.Key))
            {
                character.Stats[bonus.Key] += bonus.Value;
            }
        }
    }
}

/// <summary>
/// Human race definition
/// </summary>
public class HumanRace : CharacterRaceDefinition
{
    public override CharacterRace RaceType => CharacterRace.Human;
    public override string Name => "Human";
    public override string Description => "Versatile and adaptable";
    
    public override Dictionary<string, int> StatBonuses => new()
    {
        { "Charisma", 2 }
    };
}

/// <summary>
/// Elf race definition
/// </summary>
public class ElfRace : CharacterRaceDefinition
{
    public override CharacterRace RaceType => CharacterRace.Elf;
    public override string Name => "Elf";
    public override string Description => "Graceful and intelligent";
    
    public override Dictionary<string, int> StatBonuses => new()
    {
        { "Dexterity", 2 },
        { "Intelligence", 1 }
    };
}

/// <summary>
/// Dwarf race definition
/// </summary>
public class DwarfRace : CharacterRaceDefinition
{
    public override CharacterRace RaceType => CharacterRace.Dwarf;
    public override string Name => "Dwarf";
    public override string Description => "Sturdy and strong";
    
    public override Dictionary<string, int> StatBonuses => new()
    {
        { "Constitution", 2 },
        { "Strength", 1 }
    };
}

/// <summary>
/// Halfling race definition
/// </summary>
public class HalflingRace : CharacterRaceDefinition
{
    public override CharacterRace RaceType => CharacterRace.Halfling;
    public override string Name => "Halfling";
    public override string Description => "Quick and charming";
    
    public override Dictionary<string, int> StatBonuses => new()
    {
        { "Dexterity", 2 },
        { "Charisma", 1 }
    };
}

/// <summary>
/// Factory for creating character race definitions
/// </summary>
public static class CharacterRaceFactory
{
    private static readonly Dictionary<CharacterRace, CharacterRaceDefinition> _races = new()
    {
        { CharacterRace.Human, new HumanRace() },
        { CharacterRace.Elf, new ElfRace() },
        { CharacterRace.Dwarf, new DwarfRace() },
        { CharacterRace.Halfling, new HalflingRace() }
    };

    public static CharacterRaceDefinition GetRaceDefinition(CharacterRace raceType)
    {
        return _races[raceType];
    }

    public static IEnumerable<CharacterRaceDefinition> GetAllRaces()
    {
        return _races.Values;
    }
}
