using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using LlmTornado.Chat.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;

internal class AdventureGeneratorRunnable : OrchestrationRunnable<string, bool>
{
    TornadoApi _client;
    TornadoAgent _agent;
    public AdventureGeneratorRunnable(TornadoApi client,Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        string instructions = @$" You are an expert DnD type adventure generator. Your job is to generate a complete DnD type adventure in markdown format based on the provided theme.
In the adventure, you should include the following sections:
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
";
        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5, "Adventure Generator", instructions, outputSchema: typeof(FantasyAdventureResult), options: new Chat.ChatRequest() { Temperature = 0.3});
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<string, bool> input)
    {
        var theme = input.Input;
        var result = await _agent.Run(theme);
        FantasyAdventureResult? adventureResult = await result.Messages.Last().Content.SmartParseJsonAsync<FantasyAdventureResult>(_agent);
        if(adventureResult == null)
        {
            Console.WriteLine("Failed to generate adventure.");
            return false;
        }
        string fileName = "adventure.json";
        string savePath = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedAdventures", $"{adventureResult.Title.Replace(",", "_").Replace(":", "_").Replace(" ", "_")}_{DateTime.Now.ToString("yyyyMMdd")}");
        adventureResult.SerializeToFile(Path.Combine(savePath, fileName));
        Console.WriteLine($"Adventure markdown file generated: {fileName}");
        Console.WriteLine(adventureResult.ToString());
        return true;
    }
}
