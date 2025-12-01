using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Game;
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
        _dungeonMaster = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5, tools: [RollD20, _gameState.MovePlayer], outputSchema:typeof(FantasyDMResult));
        _memory = new PersistentConversation(_gameState.DmMemoryFile, true);
    }
    /// <summary>
    /// Tool: Rolls a 20 sided dice and returns the result.
    /// </summary>
    /// <returns></returns>
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
        Console.ForegroundColor = ConsoleColor.White;
        _maxConsoleWidth = Console.WindowWidth - _consoleMarginWidth;

        _gameState.CurrentSceneTurns++; // Increment the turn count for the current scene In the game state [GAME STATE UPDATE]

        string userActions = string.Join("\n", input.Input.ToString());


        try
        {
            _dungeonMaster.Instructions = CreateSystemMessage();
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
            return new FantasyDMResult() { Narration = "no adventure loaded", SceneCompletionPercentage = 0, NextActions = [] };
        }

        Console.WriteLine("Dm Thinking..");

        if (userActions.ToLower() != "New Game")
        {
            _memory.AppendMessage(new ChatMessage(Code.ChatMessageRoles.User, CreateNewUserPrompt(userActions)));
            _conv = await _dungeonMaster.Run(appendMessages: _memory.Messages.TakeLast(10).ToList());
        }
        else
        {
            Console.WriteLine("Starting new game...");
            _conv = await _dungeonMaster.Run(StartingNewGamePrompt(), appendMessages: _memory.Messages.TakeLast(10).ToList());
        }

        _memory.AppendMessage(_conv.Messages.Last());

        FantasyDMResult? result = await _conv.Messages.Last().Content.SmartParseJsonAsync<FantasyDMResult>(_dungeonMaster);

        if (result.HasValue)
        {
            _gameState.LatestDmResultCache = result.Value;
            if (result.Value.SceneCompletionPercentage >= 100)
            {
                _gameState.MoveToNextScene(); // [GAME STATE UPDATE]
                Console.WriteLine("\n--- Scene Complete! Moving to the next scene... ---\n");
            }

            if(_gameState.CurrentTimeOfDay < result.Value.TimeOfDay)
            {
               int timeDelta = result.Value.TimeOfDay - _gameState.CurrentTimeOfDay;
               _gameState.ProgressTime(timeDelta); // [GAME STATE UPDATE]
            }
            else if(_gameState.CurrentTimeOfDay > result.Value.TimeOfDay)
            {
               int progressedTime = (24 - _gameState.CurrentTimeOfDay) + result.Value.TimeOfDay;
               _gameState.ProgressTime(progressedTime); // [GAME STATE UPDATE]
            }

            if (_gameState.EnableTts)
            {
                try
                {
                    await TTS_Controller.CreateTTS(result.Value.Narration);
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
            - You Must use the tool to move the player to a new location when it is required to move them.
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
            {_gameState.GetNextScene()}

            Current Game Memory:
            {memoryContent}
            """;
    }
}
