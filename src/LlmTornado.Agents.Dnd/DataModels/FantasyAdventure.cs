using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using System.ComponentModel;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public class FantasyAdventure
{
    [Description("The title of the Adventure")]
    public string Title { get; set; }

    [Description("A brief overview of the Adventure")]
    public string Overview { get; set; }

    [Description("The acts within the Adventure")]
    public FantasyAct[] Acts { get; set; }

    [Description("The locations within the Adventure")]
    public FantasyLocation[] Locations { get; set; }

    [Description("The NPCs within the Adventure")]
    public FantasyNPC[] NPCs { get; set; }

    [Description("The Items within the Adventure")]
    public FantasyItem[] Items { get; set; }

    [Description("Information about the player's starting point in the Adventure (location, inventory, name, background, etc.). in Markdown format.")]
    public FantasyPlayerStartingInfo PlayerStartingInfo { get; set; }

    public void SerializeToFile(string filePath)
    {
        // Serialize the AdventureResult to a JSON file
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(filePath, json);
    }

    public FantasyAdventure DeserializeFromFile(string filePath)
    {
        // Deserialize the AdventureResult from a JSON file
        var json = System.IO.File.ReadAllText(filePath);
        FantasyAdventure obj = System.Text.Json.JsonSerializer.Deserialize<FantasyAdventure>(json);
        if (obj != null)
        {
            Title = obj.Title;
            Overview = obj.Overview;
            Acts = obj.Acts;
            Locations = obj.Locations;
            NPCs = obj.NPCs;
            Items = obj.Items;
            PlayerStartingInfo = obj.PlayerStartingInfo;
        }
        return obj;
    }

    public override string ToString()
    {
        //Return a full result of the Adventure
        return @$"
Title: {Title}

Overview: {Overview}

Acts: {string.Join("\n", Acts.Select(a => a.Title))}

Locations: {string.Join(", ", Locations.Select(l => l.Name))}

NPCs: {string.Join(", ", NPCs.Select(n => n.Name))}

Items: {string.Join(", ", Items.Select(i => i.Name))}";
    }
}
