namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Base class for character class definitions
/// </summary>
public abstract class CharacterClassDefinition
{
    public abstract CharacterClass ClassType { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Dictionary<string, int> StatModifiers { get; }
    public abstract List<string> StartingAbilities { get; }
    public abstract List<string> StartingItems { get; }

    /// <summary>
    /// Apply this class's modifiers to a character's stats
    /// </summary>
    public void ApplyToCharacter(PlayerCharacter character)
    {
        foreach (var modifier in StatModifiers)
        {
            if (character.Stats.ContainsKey(modifier.Key))
            {
                character.Stats[modifier.Key] = modifier.Value;
            }
        }

        character.Abilities.AddRange(StartingAbilities);
        character.Inventory.AddRange(StartingItems);
    }
}

/// <summary>
/// Warrior class definition
/// </summary>
public class WarriorClass : CharacterClassDefinition
{
    public override CharacterClass ClassType => CharacterClass.Warrior;
    public override string Name => "Warrior";
    public override string Description => "Strong melee fighter";
    
    public override Dictionary<string, int> StatModifiers => new()
    {
        { "Strength", 15 },
        { "Constitution", 14 }
    };
    
    public override List<string> StartingAbilities => new() { "Power Strike" };
    public override List<string> StartingItems => new() { "Iron Sword" };
}

/// <summary>
/// Mage class definition
/// </summary>
public class MageClass : CharacterClassDefinition
{
    public override CharacterClass ClassType => CharacterClass.Mage;
    public override string Name => "Mage";
    public override string Description => "Powerful spellcaster";
    
    public override Dictionary<string, int> StatModifiers => new()
    {
        { "Intelligence", 16 },
        { "Wisdom", 13 }
    };
    
    public override List<string> StartingAbilities => new() { "Fireball" };
    public override List<string> StartingItems => new() { "Wooden Staff" };
}

/// <summary>
/// Rogue class definition
/// </summary>
public class RogueClass : CharacterClassDefinition
{
    public override CharacterClass ClassType => CharacterClass.Rogue;
    public override string Name => "Rogue";
    public override string Description => "Stealthy and quick";
    
    public override Dictionary<string, int> StatModifiers => new()
    {
        { "Dexterity", 16 },
        { "Charisma", 13 }
    };
    
    public override List<string> StartingAbilities => new() { "Sneak Attack" };
    public override List<string> StartingItems => new() { "Dagger" };
}

/// <summary>
/// Cleric class definition
/// </summary>
public class ClericClass : CharacterClassDefinition
{
    public override CharacterClass ClassType => CharacterClass.Cleric;
    public override string Name => "Cleric";
    public override string Description => "Healer and support";
    
    public override Dictionary<string, int> StatModifiers => new()
    {
        { "Wisdom", 15 },
        { "Constitution", 13 }
    };
    
    public override List<string> StartingAbilities => new() { "Heal" };
    public override List<string> StartingItems => new() { "Mace" };
}

/// <summary>
/// Factory for creating character class definitions
/// </summary>
public static class CharacterClassFactory
{
    private static readonly Dictionary<CharacterClass, CharacterClassDefinition> _classes = new()
    {
        { CharacterClass.Warrior, new WarriorClass() },
        { CharacterClass.Mage, new MageClass() },
        { CharacterClass.Rogue, new RogueClass() },
        { CharacterClass.Cleric, new ClericClass() }
    };

    public static CharacterClassDefinition GetClassDefinition(CharacterClass classType)
    {
        return _classes[classType];
    }

    public static IEnumerable<CharacterClassDefinition> GetAllClasses()
    {
        return _classes.Values;
    }
}
