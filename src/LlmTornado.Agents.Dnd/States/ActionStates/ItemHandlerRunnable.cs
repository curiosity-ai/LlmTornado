using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Chat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;

internal class ItemHandlerRunnable : OrchestrationRunnable<FantasyDMResult, bool>
{
    private readonly FantasyWorldState _worldState;
    private readonly TornadoApi _client;
    private object lockObject = new object();
    public ItemHandlerRunnable(FantasyWorldState worldState, TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _worldState = worldState;
        _client = client;
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<FantasyDMResult, bool> input)
    {
        //List<Task> tasks = new List<Task>();
        //foreach (var action in input.Input.Actions)
        //{
        //    if(action.ActionType == FantasyActionType.GetItem || action.ActionType == FantasyActionType.LoseItem)
        //        tasks.Add(HandleItemAction(action, input.Input.Narration));
        //}
        //await Task.WhenAll(tasks);
        return true;
    }

    public async Task<bool> HandleItemAction(FantasyActionContent action, string narration)
    {
        string instructions = @$" You are an expert item extractor. You job is to extract the item from the content and provide a Name and description for the Item. 
The player can only lose an item if it has it.
The player already has the following items:
Inventory:
{string.Join(",\n", _worldState.Player.Inventory) + "\n"}

Game Master Narration:
{narration}
";
        TornadoAgent agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5,"Item Handler", instructions, outputSchema:typeof(DetectedFantasyItems));
        var conv = await agent.Run(action.ActionContent);
        DetectedFantasyItems? detected = await conv.Messages.Last().Content.SmartParseJsonAsync<DetectedFantasyItems>(agent);
        if (detected.HasValue)
        {
            lock (lockObject)
            {
                foreach (var item in detected.Value.ItemsGained)
                {
                    Console.WriteLine($"Getting item: {item.Name} - {item.Description}");

                    _worldState.Player.Inventory.Add(new FantasyItem(item.Name, item.Description));
                }

                foreach (var losing in detected.Value.ItemsLost)
                {
                    if (_worldState.Player.Inventory.Any(item => item.Name.Contains(losing.Name)))
                    {
                        Console.WriteLine($"Losing Item: {losing.Name} - {losing.Description}");

                        _worldState.Player.Inventory.RemoveAll(item => item.Name == losing.Name);
                    }

                }
            }
        }
        else
        {
            return false;
        }
        return true;
    }
}
