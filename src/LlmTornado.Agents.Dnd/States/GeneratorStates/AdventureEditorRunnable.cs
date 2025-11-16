using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using LlmTornado.Agents.Dnd.FantasyGenerator;
using LlmTornado.Agents.Dnd.Utility;
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

internal class AdventureEditorRunnable : OrchestrationRunnable<bool, bool>
{
    TornadoApi _client;
    TornadoAgent _agent;
    MCPServer _markdownTool;
    bool _initialized = false;

    public AdventureEditorRunnable(TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        string instructions = @$" You are an expert DnD type adventure generator. Your job is to Update the first draft of a complete DnD type adventure in markdown format based on the users feedback loop.
You Will be updating the existing markdown document to add the full adventure content based on the provided overview.. Found Here {Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md")}
In the adventure, you should have the following sections:
# Adventure Title
# Overview
# Acts and Scenes
# Locations
# Items
# Non-Player Characters (NPCs)
# Player Starting Information

There is no player or character stats or modifiers, just the adventure content. There is no Combat mechanics. Only the adventure decision content.

Try to include features game features such as fire counters if needing to escape a burning building, or tracking light sources if exploring dark caves.

Each section should be well-detailed and formatted in markdown. Use headings, subheadings, bullet points, and other markdown features to enhance readability.
The adventure should be engaging, imaginative, and suitable for a DnD campaign.

Summarize the adventure in a concise manner at the end of the generation.
";
        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5, "Adventure Generator", instructions, options: new Chat.ChatRequest() { Temperature = 0.3 });
    }

    public override async ValueTask InitializeRunnable()
    {
        if (_initialized)
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
        string instructions = @$" You are an expert DnD type adventure generator. Your job is to Update the first draft of a complete DnD type adventure in markdown format based on the users feedback loop.
You Will be updating the existing markdown document to add the full adventure content based on the provided overview.. Found Here {Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md")}
In the adventure, you should have the following sections:
# Adventure Title
# Overview
# Acts and Scenes
# Locations
# Items
# Non-Player Characters (NPCs)
# Player Starting Information

There is no player or character stats or modifiers, just the adventure content. There is no Combat mechanics. Only the adventure decision content.

Try to include features game features such as fire counters if needing to escape a burning building, or tracking light sources if exploring dark caves.

Each section should be well-detailed and formatted in markdown. Use headings, subheadings, bullet points, and other markdown features to enhance readability.
The adventure should be engaging, imaginative, and suitable for a DnD campaign.

Summarize your changes at the end of each session.
";
        Conversation feedbackResult;
        _agent.Instructions = instructions;
        string markdown = File.ReadAllText(Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md"));
        ConsoleWrapText.WriteLines(markdown, Console.WindowWidth - 20);
        Console.WriteLine("AI is processing the markdown. Please wait...");
        feedbackResult = await _agent.Run("Process the following markdown and be prepared to help the user make edits as needed. Do not make changes yet until the user ask you to do so.");
        Console.WriteLine("Ai is ready to accept your feedback.");
        Console.WriteLine("\nYou can provide more feedback or type 'done' to finish.");
        string userFeedback = Console.ReadLine() ?? "";
        

        while (userFeedback.ToLower() != "done")
        {
            feedbackResult = await _agent.Run(userFeedback, maxTurns:50);
            ConsoleWrapText.WriteLines(feedbackResult.Messages?.Last()?.GetMessageContent(), Console.WindowWidth - 20);
            Console.WriteLine("Feedback applied. You can provide more feedback or type 'done' to finish.");
            Console.Write("User: ");
            Console.Out.Flush();
            userFeedback = Console.ReadLine() ?? "";
        }
        Console.WriteLine("Adventure generation completed.");
        
        return true;
    }
   
}
