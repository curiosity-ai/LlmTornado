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

internal class DMRunnable : OrchestrationRunnable<string, FantasyDMResult>
{
    TornadoApi _client;
    TornadoAgent DMAgent;
    FantasyWorldState _worldState;
    PersistentConversation _longTermMemory;
    string latestPrompt = "";

    Conversation conv;

    public DMRunnable(FantasyWorldState worldState,TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        _worldState = worldState;
        DMAgent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Mini, tools: [RollD20, _worldState.ChangeLocation], outputSchema:typeof(FantasyDMResult));
        _longTermMemory = new PersistentConversation(_worldState.DmMemoryFile, true);
    }

    [Description("Rolls a 20 sided dice and returns the result as a string.")]
    public string RollD20()
    {
        Random rand = new Random();
        int roll = rand.Next(9, 21);
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


    public override async ValueTask<FantasyDMResult> Invoke(RunnableProcess<string, FantasyDMResult> input)
    {
        _worldState.CurrentSceneTurns++;

        try
        {
            DMAgent.Instructions = CreateSystemMessage();
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
            return new FantasyDMResult() { Narration = "no adventure loaded", SceneCompletionPercentage = 0, NextActions = [] };
        }

        string userActions = string.Join("\n", input.Input.ToString());

        if (userActions.ToLower() != "New Game")
        {
            latestPrompt = CreateNewUserPrompt(userActions);
            _longTermMemory.AppendMessage(new ChatMessage(Code.ChatMessageRoles.User, latestPrompt));
            Console.WriteLine("Dm Thinking..");
            conv = await DMAgent.Run(appendMessages: _longTermMemory.Messages.TakeLast(10).ToList());
        }
        else
        {
            Console.WriteLine("Starting new game...");
            Console.WriteLine("Dm Thinking..");
            conv = await DMAgent.Run(StartingNewGamePrompt(), appendMessages: _longTermMemory.Messages.TakeLast(10).ToList());
        }

        _longTermMemory.AppendMessage(conv.Messages.Last());

        FantasyDMResult? result = await conv.Messages.Last().Content.SmartParseJsonAsync<FantasyDMResult>(DMAgent);

        if (result.HasValue)
        {
            if (result.Value.SceneCompletionPercentage >= 100)
            {
                _worldState.MoveToNextScene();
                Console.WriteLine("\n--- Scene Complete! Moving to the next scene... ---\n");
            }

            if(_worldState.CurrentTimeOfDay < result.Value.TimeOfDay)
            {
               int timeDelta = result.Value.TimeOfDay - _worldState.CurrentTimeOfDay;
               _worldState.ProgressTime(timeDelta);
            }
            else if(_worldState.CurrentTimeOfDay > result.Value.TimeOfDay)
            {
               int progressedTime = (24 - _worldState.CurrentTimeOfDay) + result.Value.TimeOfDay;
               _worldState.ProgressTime(progressedTime);
            }

            await TTS(result.Value.Narration);

            return result.Value;
        }
        else
        {
            throw new Exception("Failed to parse DM result from agent response.");
        }
    }

    public async Task TTS(string text)
    {

        SpeechTtsResult? result = await _client.Audio.CreateSpeech(new SpeechRequest
        {
            Input = text,
            Model = AudioModel.OpenAi.Gpt4.Gpt4OMiniTts,
            ResponseFormat = SpeechResponseFormat.Mp3,
            Voice = SpeechVoice.Ash,
            Instructions = @"Voice Affect: Low, hushed, and suspenseful; convey tension and intrigue.

Tone: Deeply serious and mysterious, maintaining an undercurrent of unease throughout.

Pacing: Fast, deliberate.

Emotion: Restrained yet intense—voice should subtly tremble or tighten at key suspenseful points.

Emphasis: Highlight sensory descriptions (""footsteps echoed,"" ""heart hammering,"" ""shadows melting into darkness"") to amplify atmosphere.

Pronunciation: Slightly elongated vowels and softened consonants for an eerie, haunting effect.

Pauses: Insert meaningful pauses after phrases like ""only shadows melting into darkness,"" and especially before the final line, to enhance suspense dramatically."
        });

        if (result is not null)
        {
            await result.SaveAndDispose("ttsdemo.mp3");
        }
    }

    public string CreateNewUserPrompt(string userActions)
    {
        return @$"
Current Time in day: {_worldState.CurrentTimeOfDay}
Current Day: {_worldState.CurrentDay}
Total Hours Since Last Rest: {_worldState.HoursSinceLastRest}
Player in sleepable location: {_worldState.CurrentLocation.CanRestHere}

Total turns taken this scene (try to limit to 15): {_worldState.CurrentSceneTurns}

Player Response:
{userActions}";
    }

    public string StartingNewGamePrompt()
    {
        return @$"
You are about to start a new Dungeons and Dragons game. Begin by setting the scene and introducing the player to the world they are about to explore. 
Provide vivid descriptions and immerse the player in the adventure from the very beginning.

Players Starting Point:
Name: {_worldState.Adventure.PlayerStartingInfo.Name}
Background: {_worldState.Adventure.PlayerStartingInfo.Background}
Starting Location:
{_worldState.Adventure.Locations.Where(l => l.Id == _worldState.Adventure.PlayerStartingInfo.StartingLocationId).FirstOrDefault().ToString() ?? "Cannot find starting location"}
Inventory:
{string.Join("\n", _worldState.Adventure.PlayerStartingInfo.StartingInventory.Select(i => $"- {i}"))}

"; 
    }

    public string CreateSystemMessage()
    {

        string memoryContent = File.ReadAllText(_worldState.MemoryFile);
        return $"""
            You are an experienced Dungeon Master
            Your role is to:
            - Follow the adventure structure strictly - But make sure to each scene leads to the next scene
            - Describe scenes vividly and engagingly based on the generated world
            - Respond to player actions with narrative flair
            - You always roll for the player a 20 sided dice to determine success or failure of actions
            - Control NPCs and the environment according to the adventure
            - Progress the main quest line naturally when appropriate
            - Create interesting scenarios aligned with the adventure theme
            - Make the game fun and immersive
            - If the current scene turns is over 15, try to advance the scene to avoid stagnation
            _ When Scene Progress is at 100% the scene will automatically change Scene.
            - When player has been awake for over 32 hours, limit actions to that of finding a safe place to rest
            - When player is in a location that they cannot rest in, assist in finding a safe location to rest

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

            Current Location:
            {_worldState.CurrentLocation.ToString()}

            Next Scene:
            {GetNextScene()}

            Current Game Memory:
            {memoryContent}
            """;
    }
}
