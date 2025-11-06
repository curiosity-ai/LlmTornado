using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

public struct FantasyDMResult
{
    [Description("The next narration based off the context")]
    public string Narration { get; set; }

    [Description("Actions extracted from the user input")]
    public FantasyActionContent[] Actions { get; set; }
}

[Description("Actions extracted from the user input")]
public struct FantasyActionContent
{
    [Description("The type of action being detected")]
    public FantasyActionType ActionType { get; set; }
    [Description("The content that was considered to contain the action to extract")]
    public string ActionContent { get; set; }
}

public class DMRunnable : OrchestrationRunnable<string, FantasyDMResult>
{
    TornadoApi _client;
    TornadoAgent DMAgent;
    string _adventureTitle = "The Lost Relic of Eldoria";
    string _adventureDescription = "A thrilling quest to recover a powerful ancient relic hidden deep within the treacherous ruins of Eldoria.";
    string _adventureSetting = "A high-fantasy world filled with magic, mythical creatures, and ancient civilizations.";

    public DMRunnable(TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        DMAgent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5, outputSchema:typeof(FantasyDMResult));
    }

    public override async ValueTask<FantasyDMResult> Invoke(RunnableProcess<string, FantasyDMResult> input)
    {
        string instruct = $"""
            You are an experienced Dungeon Master running the adventure: "{_adventureTitle}"
            
            Adventure Description: {_adventureDescription}
            Setting: {_adventureSetting}

            Your role is to:
            - Follow the adventure structure loosely - use it as a guide
            - Extract Actions from the User input such as
                //World Actions
                - Move, //Move

                // Item actions
                - UseItem, //When user says to use inventory item
                - GetItem, //When you need to give user item
                - DropItem, //When user says to drop inventory item

                //Party Actions
                - ActorJoinsParty, //When you decide the NPC in the game need to follow along
                - ActorLeavesParty, //When you decide the NPC leaves the party

            - Describe scenes vividly and engagingly based on the generated world
            - Respond to player actions with narrative flair
            - Control NPCs and the environment according to the adventure
            - Progress the main quest line naturally when appropriate
            - Create interesting scenarios aligned with the adventure theme
            - Decide when combat should be initiated based on encounters in the adventure
            - Make the game fun and immersive
            - STAY NEUTRAL and UNBIASED - do not favor any player or NPC
            
            Reference the generated quests, scenes, and NPCs but don't feel constrained by them.
            Use your creativity to enhance the experience.
            
            
            """;

        DMAgent.Instructions = instruct;
        
        string userActions = string.Join("\n", input.Input.Select(inp => inp.ToString()));

        Conversation conv = await DMAgent.Run(userActions);

        FantasyDMResult? result = await conv.Messages.Last().Content.SmartParseJsonAsync<FantasyDMResult>(DMAgent);

        if (result.HasValue)
        {
            return result.Value;
        }
        else
        {
            throw new Exception("Failed to parse DM result from agent response.");
        }
    }
}
