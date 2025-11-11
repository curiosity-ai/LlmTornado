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

internal class DMRunnable : OrchestrationRunnable<string, FantasyDMResult>
{
    TornadoApi _client;
    TornadoAgent DMAgent;
    FantasyWorldState _worldState;
    PersistentConversation _longTermMemory;
    public DMRunnable(FantasyWorldState worldState,TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        _worldState = worldState;
        DMAgent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Nano, tools: [RollD20], outputSchema:typeof(FantasyDMResult));
        _longTermMemory = new PersistentConversation($"DM_{_worldState.AdventureFile.Replace(".md", "_")}LongTermMemory.json", true);
    }

    [Description("Rolls a 20 sided dice and returns the result as a string.")]
    public string RollD20()
    {
        Random rand = new Random();
        int roll = rand.Next(1, 21);
        Console.WriteLine($"[Dice Roll] Rolled a d20 and got: {roll}");
        return roll.ToString();
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

    private string NextActOverview()
    {
        if (_worldState.CurrentAct + 1 < _worldState.Adventure.Acts.Count())
        {
            return _worldState.Adventure.Acts[_worldState.CurrentAct + 1].Overview;
        }
        else
        {
            return "End of Adventure";
        }
    }

    public override async ValueTask<FantasyDMResult> Invoke(RunnableProcess<string, FantasyDMResult> input)
    {
        string memoryContent = File.ReadAllText(_worldState.MemoryFile);
        string instruct = $"""
            You are an experienced Dungeon Master
            Your role is to:
            - Follow the adventure structure loosely - use it as a guide
            - Describe scenes vividly and engagingly based on the generated world
            - Respond to player actions with narrative flair
            - You always roll for the player a 20 sided dice to determine success or failure of actions
            - Control NPCs and the environment according to the adventure
            - Progress the main quest line naturally when appropriate
            - Create interesting scenarios aligned with the adventure theme
            - Make the game fun and immersive
            - STAY NEUTRAL and UNBIASED - do not favor any player or NPC
            
            Reference the generated quests, scenes, and NPCs but don't feel constrained by them.
            Use your creativity to enhance the experience.
            
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

            Next Scene:
            {GetNextScene()}

            Next Act Overview:
            {NextActOverview()}

            Current Game Memory:
            {memoryContent}
            """;

        DMAgent.Instructions = instruct;

        string userActions = string.Join("\n", input.Input.ToString());

        Conversation conv;

        if (userActions.ToLower() != "start new game")
        {
            _longTermMemory.AppendMessage(new ChatMessage(Code.ChatMessageRoles.User, userActions));
            Console.WriteLine("DM Thinking...");
            conv = await DMAgent.Run(userActions, appendMessages: _longTermMemory.Messages.TakeLast(10).ToList());
        }
        else
        {
          Console.WriteLine("Starting new game...");
          string startPrompt
                = @$"
You are about to start a new Dungeons and Dragons game. Begin by setting the scene and introducing the player to the world they are about to explore. 
Provide vivid descriptions and immerse the player in the adventure from the very beginning.

Players Starting Point:
Name: {_worldState.Adventure.PlayerStartingInfo.Name}
Background: {_worldState.Adventure.PlayerStartingInfo.Background}
Starting Location:
{_worldState.Adventure.Locations.Where(l=>l.Id== _worldState.Adventure.PlayerStartingInfo.StartingLocationId).FirstOrDefault().ToString() ?? "Cannot find starting location"}
Inventory:
{string.Join("\n", _worldState.Adventure.PlayerStartingInfo.StartingInventory.Select(i=>$"- {i}"))}

";
            conv = await DMAgent.Run("start game", appendMessages: _longTermMemory.Messages.TakeLast(10).ToList());
        }

       
        _longTermMemory.AppendMessage(conv.Messages.Last());

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
