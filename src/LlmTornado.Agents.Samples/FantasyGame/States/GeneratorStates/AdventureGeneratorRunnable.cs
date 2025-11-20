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
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;

internal class AdventureGeneratorRunnable : OrchestrationRunnable<bool, bool>
{
    TornadoApi _client;
    TornadoAgent _agent;
    MCPServer _markdownTool;
    bool _initialized = false;

    public AdventureGeneratorRunnable(TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        string instructions = @$"
#  DnD type adventure generator

## OVERVIEW

ou are an expert DnD type adventure generator. Your job is to generate a complete DnD type adventure in markdown format based on the provided overview.
You Will be updating the existing markdown document to add the full adventure content based on the provided overview.. Found Here {Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md")}
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

Each section should be well-detailed and formatted in markdown. Use headings, subheadings, bullet points, and other markdown features to enhance readability.
The adventure should be engaging, imaginative, and suitable for a DnD Type campaign.

Summarize the adventure in a concise manner at the end of the generation.
";
        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5, "Adventure Generator", instructions, options: new Chat.ChatRequest() { Temperature = 0.3});
    }

    public override async ValueTask InitializeRunnable()
    {
        if(_initialized)
        {
            return;
        }
        _markdownTool = new MCPServer(
            "markdown-editor", "uvx", arguments: new string[] { "--from", "quantalogic-markdown-mcp", "python", "-m", "quantalogic_markdown_mcp.mcp_server" },
            allowedTools: ["load_document", "insert_section", "delete_section", "update_section", "get_section", "list_sections", "move_section", "get_document", "save_document", "analyze_document"]);
        await _markdownTool.InitializeAsync();

        _agent.AddTool(_markdownTool.AllowedTornadoTools.ToArray());
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<bool, bool> input)
    {
        string instructions = @$"
#  DnD type adventure generator

## OVERVIEW

ou are an expert DnD type adventure generator. Your job is to generate a complete DnD type adventure in markdown format based on the provided overview.
You Will be updating the existing markdown document to add the full adventure content based on the provided overview.. Found Here {Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md")}
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

Each section should be well-detailed and formatted in markdown. Use headings, subheadings, bullet points, and other markdown features to enhance readability.
The adventure should be engaging, imaginative, and suitable for a DnD Type campaign.

Summarize the adventure in a concise manner at the end of the generation.
";
        _agent.Instructions = instructions;
        Console.WriteLine("Starting adventure generation...");
        string markdownContent = File.ReadAllText(Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath,"adventure.md"));
        var result = await _agent.Run(markdownContent, maxTurns: 50);
        Console.WriteLine("Adventure generation completed.");
        Console.WriteLine(result.Messages.Last().GetMessageContent());
        Console.Out.Flush();
        return true;
    }
}
