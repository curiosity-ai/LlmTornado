using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using LlmTornado.Agents.Dnd.FantasyGenerator;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Mcp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;

internal class AdventureExtractionRunnable : OrchestrationRunnable<bool, bool>
{
    TornadoApi _client;
    TornadoAgent _agent;

    public AdventureExtractionRunnable(TornadoApi client,Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        string instructions = @$" 

#  DnD type adventure generator

## OVERVIEW

You are an expert  DnD type adventure generator. Your job is to generate a complete DnD type adventure based on the following generated Markdown Context.
You Will be referencing the existing markdown document to add the full adventure content based on the provided context and also Found Here {Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md")}
It is okay to expand upon the existing content in the markdown to create a more detailed adventure.

## Content Requirements

In the adventure, you should include the following sections:

- Adventure Title
- Overview
- Acts and Scenes
- Locations
- Items
- Non-Player Characters (NPCs)
- Player Starting Information

## ADJUSTED RULES DETAILS
- There is no player or character stats or modifiers, just the adventure content. 
- There is no Combat mechanics. Only the adventure decision content.

## ADDITIONAL FEATURES
Try to include features game features such as:
- fire counters if needing to escape a burning building, or tracking light sources if exploring dark caves.
- environmental hazards like collapsing ceilings or unstable ground.

The adventure should be engaging, imaginative, and suitable for a DnD Type campaign.
";
        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5, "Adventure Generator", instructions, outputSchema: typeof(FantasyAdventureResult), options: new Chat.ChatRequest() { Temperature = 0.3});
    }


    public override async ValueTask<bool> Invoke(RunnableProcess<bool, bool> input)
    {
        string instructions = @$" 

#  DnD type adventure generator

## OVERVIEW

You are an expert  DnD type adventure generator. Your job is to generate a complete DnD type adventure based on the following generated Markdown Context.
You Will be referencing the existing markdown document to add the full adventure content based on the provided context and also Found Here {Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md")}
It is okay to expand upon the existing content in the markdown to create a more detailed adventure.

## Content Requirements

In the adventure, you should include the following sections:

- Adventure Title
- Overview
- Acts and Scenes
- Locations
- Items
- Non-Player Characters (NPCs)
- Player Starting Information

## ADJUSTED RULES DETAILS
- There is no player or character stats or modifiers, just the adventure content. 
- There is no Combat mechanics. Only the adventure decision content.

## ADDITIONAL FEATURES
Try to include features game features such as:
- fire counters if needing to escape a burning building, or tracking light sources if exploring dark caves.
- environmental hazards like collapsing ceilings or unstable ground.

The adventure should be engaging, imaginative, and suitable for a DnD Type campaign.
";
        _agent.Instructions = instructions;
        Console.WriteLine("Extracting adventure to structured JSON...");
        string markdownContent = File.ReadAllText(Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md"));
        var result = await _agent.Run(markdownContent);
        FantasyAdventureResult? adventureResult = await result.Messages.Last().Content.SmartParseJsonAsync<FantasyAdventureResult>(_agent);
        if(adventureResult == null)
        {
            Console.WriteLine("Failed to generate adventure.");
            return false;
        }

        if (!Directory.Exists(FantasyGeneratorConfiguration.CurrentAdventurePath))
        {
            Directory.CreateDirectory(FantasyGeneratorConfiguration.CurrentAdventurePath);
        }
        adventureResult.SerializeToFile(Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.json"));
        Console.WriteLine($"Adventure JSON file generated: adventure.json");
        Console.WriteLine(adventureResult.ToString());
        return true;
    }
}
