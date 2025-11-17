using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Utility;
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
    TornadoAgent _dungeonMaster;
    FantasyWorldState _gameState;
    PersistentConversation _memory;
    string _latestPrompt = "";
    int _maxConsoleWidth = 200;
    int _consoleMarginWidth = 25;
    Conversation _conv;

    public DMRunnable(FantasyWorldState worldState,TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        _gameState = worldState;
        _dungeonMaster = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Mini, tools: [RollD20, _gameState.ChangeLocation], outputSchema:typeof(FantasyDMResult));
        _memory = new PersistentConversation(_gameState.DmMemoryFile, true);
    }

    [Description("Rolls a 20 sided dice and returns the result as a string.")]
    public string RollD20()
    {
        Random rand = new Random();
        int roll = rand.Next(9, 21);
        Console.WriteLine($"[Dice Roll] Rolled a d20 and got: {roll}");
        return roll.ToString();
    }


    public override async ValueTask<FantasyDMResult> Invoke(RunnableProcess<string, FantasyDMResult> input)
    {
        _gameState.CurrentSceneTurns++;
        _maxConsoleWidth = Console.WindowWidth - _consoleMarginWidth;
        Console.ForegroundColor = ConsoleColor.White;

        try
        {
            _dungeonMaster.Instructions = CreateSystemMessage();
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
            return new FantasyDMResult() { Narration = "no adventure loaded", SceneCompletionPercentage = 0, NextActions = [] };
        }

        string userActions = string.Join("\n", input.Input.ToString());

        if (userActions.ToLower() != "New Game")
        {
            _latestPrompt = CreateNewUserPrompt(userActions);
            _memory.AppendMessage(new ChatMessage(Code.ChatMessageRoles.User, _latestPrompt));
            Console.WriteLine("Dm Thinking..");
            _conv = await _dungeonMaster.Run(appendMessages: _memory.Messages.TakeLast(10).ToList());
        }
        else
        {
            Console.WriteLine("Starting new game...");
            Console.WriteLine("Dm Thinking..");
            _conv = await _dungeonMaster.Run(StartingNewGamePrompt(), appendMessages: _memory.Messages.TakeLast(10).ToList());
        }

        _memory.AppendMessage(_conv.Messages.Last());

        FantasyDMResult? result = await _conv.Messages.Last().Content.SmartParseJsonAsync<FantasyDMResult>(_dungeonMaster);

        if (result.HasValue)
        {
            _gameState.LatestDmResultCache = result.Value;
            if (result.Value.SceneCompletionPercentage >= 100)
            {
                _gameState.MoveToNextScene();
                Console.WriteLine("\n--- Scene Complete! Moving to the next scene... ---\n");
            }

            if(_gameState.CurrentTimeOfDay < result.Value.TimeOfDay)
            {
               int timeDelta = result.Value.TimeOfDay - _gameState.CurrentTimeOfDay;
               _gameState.ProgressTime(timeDelta);
            }
            else if(_gameState.CurrentTimeOfDay > result.Value.TimeOfDay)
            {
               int progressedTime = (24 - _gameState.CurrentTimeOfDay) + result.Value.TimeOfDay;
               _gameState.ProgressTime(progressedTime);
            }

            if (_gameState.EnableTts)
            {
                try
                {
                    await TTS(result.Value.Narration);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TTS generation failed: {ex.Message}]");
                }
            }

            Console.Write("\n[Dungeon Master]:\n\n");
            ConsoleWrapText.WriteLines(_gameState.LatestDmResultCache.Narration, _maxConsoleWidth);
            Console.Out.Flush(); // Force the buffered output to be displayed immediately

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

Pronunciation: Clear and cunning.

Pauses: Limit pausing to keep up pace but Insert meaningful pauses for dramatic moments."
        });

        if (result is not null)
        {
            await result.SaveAndDispose("ttsdemo.mp3");
        }
    }

    public string CreateNewUserPrompt(string userActions)
    {
        return @$"
Current Time in day: {_gameState.CurrentTimeOfDay}
Current Day: {_gameState.CurrentDay}
Total Hours Since Last Rest: {_gameState.HoursSinceLastRest}
Player in sleepable location: {_gameState.CurrentLocation.CanRestHere}

Total turns taken this scene (try to limit to 15): {_gameState.CurrentSceneTurns}

Player Response:
{userActions}";
    }

    public string StartingNewGamePrompt()
    {
        return @$"
You are about to start a new Dungeons and Dragons game. Begin by setting the scene and introducing the player to the world they are about to explore. 
Provide vivid descriptions and immerse the player in the adventure from the very beginning.

Players Starting Point:
Name: {_gameState.Adventure.PlayerStartingInfo.Name}
Background: {_gameState.Adventure.PlayerStartingInfo.Background}
Starting Location:
{_gameState.Adventure.Locations.Where(l => l.Id == _gameState.Adventure.PlayerStartingInfo.StartingLocationId).FirstOrDefault().ToString() ?? "Cannot find starting location"}
Inventory:
{string.Join("\n", _gameState.Adventure.PlayerStartingInfo.StartingInventory.Select(i => $"- {i}"))}

"; 
    }

    public string CreateSystemMessage()
    {

        string memoryContent = File.ReadAllText(_gameState.MemoryFile);
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
            {_gameState.Adventure.Overview}

            Current Act:
            {_gameState.Adventure.Acts[_gameState.CurrentActIndex].Title}

            Current Overview:
            {_gameState.Adventure.Acts[_gameState.CurrentActIndex].Overview}

            Act Progression:
            {_gameState.CurrentSceneIndex / _gameState.Adventure.Acts[_gameState.CurrentActIndex].Scenes.Count()}

            Current Scene:
            {_gameState.Adventure.Acts[_gameState.CurrentActIndex].Scenes[_gameState.CurrentSceneIndex]}

            Current Location:
            {_gameState.CurrentLocation.ToString()}

            Next Scene:
            {GetNextScene()}

            Current Game Memory:
            {memoryContent}
            """;
    }
}
