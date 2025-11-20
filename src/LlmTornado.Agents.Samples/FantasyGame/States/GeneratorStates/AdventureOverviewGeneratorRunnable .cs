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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;

public struct AdventureOverviewResult
{
    [Description("The title of the adventure")]
    public string AdventureTitle { get; set; }

    [Description("A concise overview of the adventure")]
    public string Overview { get; set; }
}

internal class AdventureOverviewGeneratorRunnable : OrchestrationRunnable<string, bool>
{
    TornadoApi _client;
    TornadoAgent _agent;
    bool _initialized = false;
    public AdventureOverviewGeneratorRunnable(TornadoApi client,Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        string instructions = @$" You are an expert DnD type adventure generator. Your job is to generate a complete DnD type adventure in markdown format based on the provided theme.
In the adventure, you should include the following sections:
# Adventure Title
# Overview

There is no player or character stats or modifiers, just the adventure content. There is no Combat mechanics. Only the adventure decision content.

Try to include features game features such as fire counters if needing to escape a burning building, or tracking light sources if exploring dark caves.

Each section should be well-detailed and formatted in markdown. Use headings, subheadings, bullet points, and other markdown features to enhance readability.
The adventure should be engaging, imaginative, and suitable for a DnD campaign.

Summarize the adventure in a concise manner at the end of the generation.
";
        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Nano, "Adventure Overview Generator", instructions, outputSchema: typeof(AdventureOverviewResult), options: new Chat.ChatRequest() { Temperature = 0.3});
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<string, bool> input)
    {
        Console.WriteLine("Generating adventure overview...");
        var theme = input.Input;
        var result = await _agent.Run(theme);

        AdventureOverviewResult? overviewResult = await result.Messages.Last().Content.SmartParseJsonAsync<AdventureOverviewResult>(_agent);

        if(overviewResult == null)
        {
            Console.WriteLine("Failed to generate adventure.");
            return false;
        }

        var safeFolderName = FileNameHelpers.ToSafeFolderName(overviewResult.Value.AdventureTitle);
        var adventureRoot = Path.Combine(FantasyGeneratorConfiguration.GeneratedAdventuresFilePath, safeFolderName);
        Directory.CreateDirectory(adventureRoot);

        var manifest = AdventureRevisionManager.LoadManifest(adventureRoot);
        manifest.AdventureTitle = overviewResult.Value.AdventureTitle;
        var revisionEntry = AdventureRevisionManager.CreateRevision(adventureRoot, manifest, label: manifest.Revisions.Count == 0 ? "Initial draft" : null);
        FantasyGeneratorConfiguration.SetAdventureContext(manifest.AdventureTitle, adventureRoot, revisionEntry.RevisionId);

        Directory.CreateDirectory(FantasyGeneratorConfiguration.CurrentAdventurePath);

        using (StreamWriter sw = new StreamWriter(Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md")))
        {
            sw.WriteLine($"#  {overviewResult.Value.AdventureTitle}");
            sw.WriteLine($"##  Overview");
            sw.WriteLine($"{overviewResult.Value.Overview}");
        }
        Console.WriteLine($"Adventure markdown file generated: {Path.Combine(FantasyGeneratorConfiguration.CurrentAdventurePath, "adventure.md")}");
        Console.WriteLine(overviewResult.ToString());
        return true;
    }
}
