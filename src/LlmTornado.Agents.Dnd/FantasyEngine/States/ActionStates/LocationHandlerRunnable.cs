using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Chat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;

public struct FantasyLocationResult
{
    public string Name { get; set; }
    public string Description { get; set; }
}

internal class LocationHandlerRunnable : OrchestrationRunnable<FantasyDMResult, bool>
{
    private readonly FantasyWorldState _worldState;
    private readonly TornadoApi _client;
    private object lockObject = new object();

    public LocationHandlerRunnable(FantasyWorldState worldState, TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _worldState = worldState;
        _client = client;
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<FantasyDMResult, bool> input)
    {
        List<Task> tasks = new List<Task>();
        foreach (var action in input.Input.Actions)
        {
            if(action.ActionType == FantasyActionType.Move)
                tasks.Add(HandleAction(action, input.Input.Narration));
        }
        await Task.WhenAll(tasks);
        return true;
    }

    public async Task<bool> HandleAction(FantasyActionContent action, string narration)
    {
        string instructions = @$" You are an expert entity extractor. You job is to extract the location from the content and provide a Name and description for the new location. 
The player is currently at:
Location:
{_worldState.Locations[_worldState.CurrentLocationName].Name}
Description:
{_worldState.Locations[_worldState.CurrentLocationName].Description}

Game Master Narration:
{narration}
";
        TornadoAgent agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5,"Item Handler", instructions, outputSchema:typeof(FantasyLocationResult));
        var conv = await agent.Run(action.ActionContent);
        FantasyLocationResult? detected = await conv.Messages.Last().Content.SmartParseJsonAsync<FantasyLocationResult>(agent);
        if (detected.HasValue)
        {
            lock (lockObject)
            {
                FantasyScene currentLocation;
                if (!_worldState.Locations.ContainsKey(_worldState.CurrentLocationName))
                {
                    currentLocation = new FantasyScene(
                                    name: _worldState.CurrentLocationName,
                                    description: "Start location",
                                    connectedLocations: new List<FantasyScene>()
                                    );

                    _worldState.Locations.Add(currentLocation.Name, currentLocation);
                }
                else
                {
                    currentLocation = _worldState.Locations[_worldState.CurrentLocationName];
                }

                var newLocation = new FantasyScene
                (
                    name: detected.Value.Name,
                    description: detected.Value.Description,
                    connectedLocations: new List<FantasyScene>()
                );

                currentLocation.ConnectedLocations.Add(newLocation);
                newLocation.ConnectedLocations.Add(currentLocation);

                _worldState.Locations.Add(detected.Value.Name, newLocation);
                _worldState.CurrentLocationName = detected.Value.Name;

                Console.WriteLine($"Moving to location: {newLocation.Name} - {newLocation.Description}");
            }
        }
        else
        {
            return false;
        }
        return true;
    }
}
