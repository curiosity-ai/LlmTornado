namespace LlmTornado.Agents.Dnd.DataModels;

/// <summary>
/// Represents the initial setup choices for a new game
/// </summary>
public struct CharacterCreation
{
    public string Name { get; set; }
    public string CharacterClass { get; set; }
    public string Race { get; set; }
    public string Background { get; set; }

    public CharacterCreation(string name, string characterClass, string race, string background)
    {
        Name = name;
        CharacterClass = characterClass;
        Race = race;
        Background = background;
    }
}
