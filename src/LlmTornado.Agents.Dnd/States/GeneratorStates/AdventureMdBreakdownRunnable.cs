using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;
using LlmTornado.Chat.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;

public struct AdventureBreakdown
{
    public FantasyLocationResult[] Locations { get; set; }
    public FantasyItemResult[] Items { get; set; }
    public FantasyNpcResult[] NonPlayerCharacters { get; set; }
}

internal class AdventureMdBreakdownRunnable : OrchestrationRunnable<string, AdventureBreakdown>
{
    TornadoApi _client;
    TornadoAgent _agent;
    FantasyWorldState _worldState;
    public AdventureMdBreakdownRunnable(TornadoApi client, FantasyWorldState worldState, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        _worldState = worldState;
        string instructions = @$" 
You are an expert Data Extractor. 
Your job is to Extract the following information from the markdown and break it apart into specified chunks for further processing.
Make sure to Get all of the included sections in the markdown.
Data to extract:
# Locations
# Items
# Non-Player Characters (NPCs)
";
        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Mini, "Adventure Md Extractor", instructions, outputSchema: typeof(AdventureBreakdown));
    }

    public override async ValueTask<AdventureBreakdown> Invoke(RunnableProcess<string, AdventureBreakdown> input)
    {
        // Read from adventure file if SaveDataDirectory is set
        string theme = string.Empty;
        if (!string.IsNullOrEmpty(_worldState.SaveDataDirectory) && File.Exists(_worldState.AdventureFile))
        {
            theme = File.ReadAllText(_worldState.AdventureFile);
        }
        
        var result = await _agent.Run(theme);
        AdventureBreakdown? mdFile = await result.Messages.Last().Content.SmartParseJsonAsync<AdventureBreakdown>(_agent);
        if(mdFile == null || !mdFile.HasValue)
        {
            return new AdventureBreakdown();
        }

        return mdFile.Value;
    }
}
