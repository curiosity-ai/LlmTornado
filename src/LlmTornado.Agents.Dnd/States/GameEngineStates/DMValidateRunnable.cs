using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Audio;
using LlmTornado.Audio.Models;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

[Description("Validates the player's intended action and decides whether to allow or deny it based on game rules and context.")]
public struct DMValidateResult
{
    [Description("Reason to allow or deny the action")]
    public string Reason { get; set; }

    [Description("Whether to allow the action or not")]
    public bool AllowAction { get; set; }
}

public class DmValidationResult
{
    public DMValidateResult Result { get; set; }
    public string UserAction { get; set; }
}
internal class DMValidateRunnable : OrchestrationRunnable<string, DmValidationResult>
{
    TornadoApi _client;
    TornadoAgent DMAgent;
    FantasyWorldState _worldState;
    PersistentConversation _longTermMemory;

    Conversation conv;

    public DMValidateRunnable(FantasyWorldState worldState,TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        _worldState = worldState;
        DMAgent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Mini, outputSchema: typeof(DMValidateResult));
        _longTermMemory = new PersistentConversation(_worldState.DmMemoryFile, true);
    }

    private string GetNextScene()
    {
        var currentAct = _worldState.Adventure.Acts[_worldState.CurrentAct];
        if (_worldState.CurrentScene + 1 < currentAct.Scenes.Count())
        {
            return currentAct.Scenes[_worldState.CurrentScene + 1].ToString();
        }
        else
        {
            return "End of Act";
        }
    }

    public override async ValueTask<DmValidationResult> Invoke(RunnableProcess<string, DmValidationResult> input)
    {
        _worldState.CurrentSceneTurns++;

        try
        {
            DMAgent.Instructions = CreateSystemMessage();
        }
        catch(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = ConsoleColor.White;
            return new DmValidationResult()
            {
                Result = new DMValidateResult
                {
                    AllowAction = false,
                    Reason = "Error creating system message: " + ex.Message
                },
                UserAction = input.Input

            };
        }

        string userActions = string.Join("\n", input.Input.ToString());

        Console.WriteLine("Validating Action..");
        if (input.Input.StartsWith("/m"))
        {
            if(userActions.StartsWith("/move"))
                userActions = userActions.Replace("/move", "/m");
            var locationId = userActions.Replace("/m", "").Trim();
            if(string.IsNullOrEmpty(locationId))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[DM Validation] Move action denied: No location specified for move action.");
                Console.ForegroundColor = ConsoleColor.White;
                return new DmValidationResult()
                {
                    Result = new DMValidateResult
                    {
                        AllowAction = false,
                        Reason = "No location specified for move action."
                    },
                    UserAction = input.Input
                };
            }
            string moveAction = @$"
The player intends to move to location: {_worldState.Adventure.Locations.FirstOrDefault(l=>l.Id== locationId)?.ToString()}. 
Evaluate if this move is valid based on the current adventure state and provide reasoning.
Is the player in a location that allows travel to this new location? : {_worldState.CanChangeLocation(locationId)}  
";
            conv = await DMAgent.Run(moveAction, appendMessages: _longTermMemory.Messages.TakeLast(10).ToList());
        }
        else
        {
            conv = await DMAgent.Run(userActions, appendMessages: _longTermMemory.Messages.TakeLast(10).ToList());
        }
            

        DMValidateResult? result = await conv.Messages.Last().Content.SmartParseJsonAsync<DMValidateResult>(DMAgent);

        if (result.HasValue)
        {
            if (input.Input.StartsWith("/m"))
            {
                if (!result.Value.AllowAction)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[DM Validation] Move action denied: {result.Value.Reason}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                {
                    var locationId = userActions.Replace("/m", "").Trim();
                    var previousLocation = _worldState.CurrentLocation;
                    _worldState.ChangeLocation(locationId);
                    Console.ForegroundColor = ConsoleColor.White;
                    return new DmValidationResult()
                    {
                        Result = result.Value,
                        UserAction = $"[DM Validation] Move action approved: Player Moved from {previousLocation.Name} to {_worldState.CurrentLocation.Name}"
                    };
                }
            }
            else
            {
                if (!result.Value.AllowAction)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[DM Validation] Action denied: {result.Value.Reason}");
                    Console.ForegroundColor = ConsoleColor.White;
                }

                return new DmValidationResult()
                {
                    Result = result.Value,
                    UserAction = input.Input
                };
            }
        }

        return new DmValidationResult()
        {
            Result = new DMValidateResult
            {
                AllowAction = false,
                Reason = "Unknown error occurred while deciding action approval"
            },
            UserAction = input.Input

        };
    }

    public string CreateSystemMessage()
    {
        string memoryContent = File.ReadAllText(_worldState.MemoryFile);
        return $"""
            You are an experienced Adventure action approver.
            Your role is to evaluate the player's intended actions and decide whether to allow or deny them based on the current adventure state.
            - You must strictly adhere to the adventure structure and rules.
            - Consider the current scene, location, previous messages, and overall adventure context when evaluating actions.
            - Provide clear reasoning for your decisions.

            Current context of the adventure:
            
            Adventure Overview:
            {_worldState.Adventure.Overview}

            Current Act:
            {_worldState.Adventure.Acts[_worldState.CurrentAct].Title}

            Current Overview:
            {_worldState.Adventure.Acts[_worldState.CurrentAct].Overview}

            Act Progression:
            {_worldState.CurrentScene / _worldState.Adventure.Acts[_worldState.CurrentAct].Scenes.Count()}

            Current Scene:
            {_worldState.Adventure.Acts[_worldState.CurrentAct].Scenes[_worldState.CurrentScene]}

            Current Location:
            {_worldState.CurrentLocation.ToString()}

            Next Scene:
            {GetNextScene()}

            Current Game Memory:
            {memoryContent}
            """;
    }
}
