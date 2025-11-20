using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;

public class FantasyAdventureResult
{
    [Description("The title of the Adventure")]
    public string Title { get; set; }

    [Description("A brief overview of the Adventure")]
    public string Overview { get; set; }

    [Description("The acts within the Adventure")]
    public FantasyAdventureAct[] Acts { get; set; }

    [Description("The locations within the Adventure")]
    public FantasyAdventureLocation[] Locations { get; set; }

    [Description("The NPCs within the Adventure")]
    public FantasyAdventureNPC[] NPCs { get; set; }
    
    [Description("The Items within the Adventure")]
    public FantasyAdventureItem[] Items { get; set; }

    [Description("Information about the player's starting point in the Adventure (location, inventory, name, background, etc.). in Markdown format.")]
    public FantasyPlayerStartingInfo PlayerStartingInfo { get; set; }

    public void SerializeToFile(string filePath)
    {
        // Serialize the AdventureResult to a JSON file
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(filePath, json);
    }

    public FantasyAdventureResult DeserializeFromFile(string filePath)
    {
        // Deserialize the AdventureResult from a JSON file
        var json = System.IO.File.ReadAllText(filePath);
        FantasyAdventureResult obj = System.Text.Json.JsonSerializer.Deserialize<FantasyAdventureResult>(json);
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

    public FantasyAdventure ToFantasyAdventure()
    {
        return new FantasyAdventure()
        {
            Title = this.Title,
            Overview = this.Overview,
            Acts = Acts.Select(a => new FantasyAct
            {
                Title = a.Title,
                Overview = a.Overview,
                Scenes = a.Scenes.Select(s => new FantasyScene
                {
                    Title = s.Title,
                    Overview = s.Overview,
                    Goals = s.Goals,
                    Locations = s.LocationIds.Select(id => this.Locations.FirstOrDefault(l => l.Id == id)?.ToFantasyLocation()).Where(l => l != null).ToArray()!,
                    Outcomes = s.Outcomes,
                    HazardsAndChallenges = s.HazardsAndChallenges,
                    AdditionSceneSpecificElements = s.AdditionSceneSpecificElements,
                    ImportantNPCs = s.ImportantNPCIds != null ? s.ImportantNPCIds.Select(id => this.NPCs.FirstOrDefault(n => n.Id == id)?.ToFantasyNPC()).Where(n => n != null).ToArray()! : null
                }).ToArray()
            }).ToArray(),
            Locations = this.Locations.Select(l => l.ToFantasyLocation()).ToArray(),
            NPCs = this.NPCs.Select(n => n.ToFantasyNPC()).ToArray(),
            Items = this.Items.Select(i => i.ToFantasyItem()).ToArray(),
            PlayerStartingInfo = new FantasyPlayerStartingInfo
            {
                Name = this.PlayerStartingInfo.Name,
                Background = this.PlayerStartingInfo.Background,
                StartingLocationId = this.PlayerStartingInfo.StartingLocationId,
                StartingInventory = this.PlayerStartingInfo.StartingInventory
            }
        };
    }
}

public class FantasyPlayerStartingInfo
{
    [Description("The name of the player character.")]
    public string Name { get; set; }
    [Description("The background of the player character.")]
    public string Background { get; set; }
    [Description("The starting location Id of the player character.")]
    public string StartingLocationId { get; set; }
    [Description("The starting inventory of the player character.")]
    public string[] StartingInventory { get; set; }
    public override string ToString()
    {
        return @$"
Name: {Name}
Background: {Background}
Starting Location Id: {StartingLocationId}
Starting Inventory: {string.Join(", ", StartingInventory)}";

    }

}

public class FantasyAdventureNPC
{
    [Description("Unique identifier.")]
    public string Id { get; set; }

    [Description("The name of the NPC.")]
    public string Name { get; set; }

    [Description("The description and role of the NPC")]
    public string Description { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Name}:{Description}";
    }

    public FantasyNPC ToFantasyNPC()
    {
        return new FantasyNPC
        (
            id: this.Id,
            name: this.Name,
            description: this.Description
        );
    }
}

public class FantasyAdventureItem
{
    [Description("Unique identifier.")]
    public string Id { get; set; }

    [Description("The name of the item.")]
    public string Name { get; set; }

    [Description("The description of the item")]
    public string Description { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Name}: {Description}";
    }

    public FantasyItem ToFantasyItem()
    {
        return new FantasyItem
        (
            id: this.Id,
            name: this.Name,
            description: this.Description
        );
    }
}

public class FantasyAdventureLocation
{
    [Description("Unique identifier.")]
    public string Id { get; set; }

    [Description("The name of the location.")]
    public string Name { get; set; }

    [Description("The description of the location in Markdown Format")]
    public string Description { get; set; } = string.Empty;

    [Description("Indicates if the location is a rest location.")]
    public bool RestLocation { get; set; } = false;

    [Description("The routes from this location to other locations.")]
    public FantasyAdventureRoute[] Routes { get; set; }
    public override string ToString()
    {
        return @$"

Name: {Name}

Description: {Description}

Rest Location: {RestLocation}

Routes: 
{string.Join(",\n", Routes.Select(r => r.ToString()))}
";
    }

    public FantasyLocation ToFantasyLocation()
    {
        return new FantasyLocation
        (
            id: this.Id,
            name: this.Name,
            description: this.Description,
            restLocation: this.RestLocation, // Default to false; can be extended later
            routes: this.Routes.Select(r => new FantasyRoute
            (
                toLocationId: r.EndingLocationId,
                description: r.Description,
                distanceInHours: r.DistanceInHours
            )).ToArray()
        );
    }
}

[Description("Represents a route between two locations in the adventure.")]
public class FantasyAdventureRoute
{

    [Description("The ending location Id of the route.")]
    public string EndingLocationId { get; set; }

    [Description("A brief description of the route.")]
    public string Description { get; set; }

    [Description("The distance of the route in hours.")]
    public int DistanceInHours { get; set; }
    public override string ToString()
    {
        return @$"
Ending Location Id: {EndingLocationId}
Description: {Description}
Distance (in hours): {DistanceInHours}
";
    }

}

public class FantasyAdventureAct
{
    [Description("The title of the act.")]
    public string Title { get; set; }

    [Description("A brief overview of the act.")]
    public string Overview { get; set; }

    [Description("The scenes within the act.")]
    public FantasyAdventureScene[] Scenes { get; set; }

    //Full act details
    public override string ToString()
    {
        return @$"
Act Title: {Title}
Overview: {Overview}
Scenes: {string.Join("\n", Scenes.Select(s => s.Title))}";
    }
}

public class FantasyAdventureScene
{
    [Description("The title of the scene.")]
    public string Title { get; set; }

    [Description("A brief overview of the scene.")]
    public string Overview { get; set; }

    [Description("The goals of the scene.")]
    public string Goals { get; set; }

    [Description("The name of the locations where the scene takes place or that player can discover.")]
    public string[] LocationIds{ get; set; }

    [Description("The possible outcomes of the scene")]
    public string[] Outcomes { get; set; }

    [Description("Specific Hazards and challenges in the scene like counters or traps")]
    public string? HazardsAndChallenges { get; set; } = "N/A";  

    [Description("Additional elements to include for added depth, and fun. (e.g different routes, hidden treasures, secret events, ect)")]
    public string? AdditionSceneSpecificElements { get; set; } = "N/A";

    [Description("Important NPC Ids involved in the scene or that we meet.")]
    public string[]? ImportantNPCIds { get; set; }

    public override string ToString()
    {
        return @$"
Scene Title: {Title}
Overview: {Overview}
Goals: {Goals}
Outcomes: {string.Join(", ", Outcomes)}
Hazards and Challenges: {HazardsAndChallenges}
Additional Elements: {AdditionSceneSpecificElements}
Important NPCs: {(ImportantNPCIds != null ? string.Join(", ", ImportantNPCIds) : "N/A")}";
    }
}
