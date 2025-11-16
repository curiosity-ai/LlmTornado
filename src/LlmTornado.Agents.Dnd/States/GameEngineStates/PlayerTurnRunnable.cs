using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Dnd.Agents.Runnables;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Utility;
using LlmTornado.Audio;
using LlmTornado.Audio.Models;
using LlmTornado.Chat;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;

internal class PlayerTurnRunnable : OrchestrationRunnable<FantasyDMResult, string>
{

    private readonly FantasyWorldState _gameState;
    private readonly TornadoApi _client;
    private WaveOutEvent? _currentOutputDevice;
    int ConsoleMarginWidth = 25;
    int maxConsoleWidth = 200;

    public PlayerTurnRunnable(TornadoApi client,FantasyWorldState state, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _gameState = state;
        _client = client;
    }

    public override async ValueTask<string> Invoke(RunnableProcess<FantasyDMResult, string> input)
    {
        maxConsoleWidth = Console.WindowWidth - ConsoleMarginWidth;
        FantasyDMResult dMResult = input.Input;
        // Get player action

        ShowCurrentActions();

        // Start TTS in background
        var ttsTask = Task.Run(() => TTS());

        Console.ForegroundColor = ConsoleColor.Yellow;

        string? result = PlayerInputLoop(ttsTask);

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" \n  ");
        return result;
    }

    public void ShowCurrentActions()
    {
        Console.Write("\nAvailable Actions:\n");
        foreach (var action in _gameState.LatestDmResultCache.NextActions)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            ConsoleWrapText.WriteLines($"- {action.Action}", maxConsoleWidth);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("     Success Rate:");
            ConsoleWrapText.WriteLines($"{action.MinimumSuccessThreshold}", maxConsoleWidth);
            Console.Write($"     Duration:{action.DurationHours}Hrs\n");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("     Success Outcome: ");
            ConsoleWrapText.WriteLines($"{action.SuccessOutcome}", maxConsoleWidth);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("     Failure Outcome: ");
            ConsoleWrapText.WriteLines($"{action.FailureOutcome}", maxConsoleWidth);
        }
        Console.Out.Flush(); // Force the buffered output to be displayed immediately
        Console.ForegroundColor = ConsoleColor.White;
    }

    public string PlayerInputLoop(Task ttsTask)
    {
        string? result = "";
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\nAdditional Commands: /info, /dm, /help, /rest, /actions, /move, /repeat");
            Console.Write($"\n[Player]:");
            Console.Out.Flush(); // Force the buffered output to be displayed immediately
            result = Console.ReadLine();

            if (result.ToLower() == "/h" || result.ToLower() == "/help")
            {
                WriteHelp();
                // Don't stop audio for informational commands
            }
            else if (result.ToLower() == "/i" || result.ToLower() == "/info")
            {
                WriteWorldInfo();
                // Don't stop audio for informational commands
            }
            else if (result.ToLower() == "/d" || result.ToLower() == "/dm")
            {
                WriteDmResult();
                // Don't stop audio for informational commands
            }
            else if (result.ToLower() == "/rest" || result.ToLower() == "/r")
            {
                // Stop TTS for game actions
                if (ttsTask != null && !ttsTask.IsCompleted)
                {
                    StopTTS();
                }

                if (_gameState.Rest())
                {
                    Console.WriteLine($"Time: day {_gameState.CurrentDay} - HR: [{_gameState.CurrentTimeOfDay}] ");
                    Console.WriteLine("Resting... ");
                    Console.WriteLine($"Time: day {_gameState.CurrentDay} - HR: [{_gameState.CurrentTimeOfDay}] ");
                    result = "Player chooses to rest.";
                    break;
                }
            }
            else if (result.ToLower() == "/skip" || result.ToLower() == "/s")
            {
                // Stop TTS for game actions
                if (ttsTask != null && !ttsTask.IsCompleted)
                {
                    StopTTS();
                }
            }
            else if(result.ToLower() == "/actions" || result.ToLower() == "/a")
            {
                ShowCurrentActions();
            }
            else if (result.ToLower() == "/move" || result.ToLower() == "/m")
            {
                result = MoveRoute();
                if(result != "Failed")
                {
                    StopTTS();
                    break;
                }
            }
            else if(result.ToLower() == "/rep" || result.ToLower() == "/repeat")
            {
                // Stop TTS for game actions
                if (ttsTask != null && !ttsTask.IsCompleted)
                {
                    StopTTS();
                }
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("\n[Dungeon Master]:\n\n");
                ConsoleWrapText.WriteLines(_gameState.LatestDmResultCache.Narration, maxConsoleWidth);
                Console.Out.Flush(); // Force the buffered output to be displayed immediately
                // Restart TTS
                ttsTask = Task.Run(() => TTS());
            }
            else if (string.IsNullOrEmpty(result))
            {
                Console.WriteLine("Input cannot be empty please type ... if you dont want to respond.");
            }
            else
            {
                // Stop TTS for actual player actions
                if (ttsTask != null && !ttsTask.IsCompleted)
                {
                    StopTTS();
                }
                break;
            }
        }

        return result;
    }

    public string MoveRoute()
    {
        string result = "";
        var routes = _gameState.GetAvailableRoutes();
        Console.WriteLine("Available Routes:");
        int index = 1;
        foreach (var route in routes)
        {
            FantasyLocation location = _gameState.Adventure.Locations.First(l => l.Id == route.ToLocationId);
            if (location == null) continue;
            Console.WriteLine($"- {index}  {location.Name} = Move Time in hours: [{route.DistanceInHours}] Route Description:{route.Description}");
            index++;
        }
        Console.WriteLine("Select the number of the location you want to move to:");
        string? selectionInput = Console.ReadLine();
        if (selectionInput != null)
        {
            if (int.TryParse(selectionInput, out int selectedIndex))
            {
                if (selectedIndex >= 1 && selectedIndex <= routes.Length)
                {
                    var selectedRoute = routes[selectedIndex - 1];
                    result = "/move " + selectedRoute.ToLocationId;
                    return result;
                }
            }

        }
        return "Failed";
    }

    public string ChangeLocation(string newLocation)
    {
        return _gameState.ChangeLocation(newLocation);
    }

    public void TTS()
    {
        try
        {
            Console.WriteLine("\n[Audio playing /rest, /skip or any action to stop tts]");

            TimeSpan duration = GetWavFileDuration("ttsdemo.mp3");

            using (var audioFile = new AudioFileReader("ttsdemo.mp3"))
            {
                _currentOutputDevice = new WaveOutEvent();
                _currentOutputDevice.Init(audioFile);
                _currentOutputDevice.Play();

                while (_currentOutputDevice.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(100);
                }
                
                _currentOutputDevice?.Dispose();
                _currentOutputDevice = null;
            }
        }
        catch (Exception ex)
        {
            // Handle any TTS errors gracefully (e.g., file not found)
            Console.WriteLine($"[TTS unavailable: {ex.Message}]");
        }
    }
    
    public void StopTTS()
    {
        try
        {
            if (_currentOutputDevice != null && _currentOutputDevice.PlaybackState == PlaybackState.Playing)
            {
                _currentOutputDevice.Stop();
                Console.WriteLine("[Audio stopped]");
            }
        }
        catch (Exception ex)
        {
            // Silently handle any errors
        }
    }

    public static TimeSpan GetWavFileDuration(string fileName)
    {
        using (var audioFile = new AudioFileReader(fileName))
        {
            return audioFile.TotalTime;
        }
    }

    public void WriteHelp()
    {
        Console.WriteLine("/quit, /q or /exit, /e to exit the game.");
        Console.WriteLine("/info or /i get get world state information");
        Console.WriteLine("/dm or /d to get dm information");
        Console.WriteLine("/help or /h to get this help menu again");
        Console.WriteLine("/rest or /r to rest");
        Console.WriteLine("/skip or /s to Skip TTS");
        Console.WriteLine("/action or /a to get available actions");
        Console.WriteLine("/move or /m to move to a new location");
        Console.WriteLine("/repeat or /rep to repeat the last DM narration");
    }

    public void WriteWorldInfo()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        ConsoleWrapText.WriteLines(@$"

World Info:
# {_gameState.Adventure.Title} 

- Act {_gameState.CurrentAct + 1}: {_gameState.Adventure.Acts[_gameState.CurrentAct].Title} 

- Scene {_gameState.CurrentScene + 1}: {_gameState.Adventure.Acts[_gameState.CurrentAct].Scenes[_gameState.CurrentScene]}

- Location {_gameState.CurrentLocation.ToString()}

- Scene Turn {_gameState.CurrentSceneTurns}

- Current Time: Day  {_gameState.CurrentDay} : Hr {_gameState.CurrentTimeOfDay}

- Time awake: {_gameState.HoursSinceLastRest}

", maxConsoleWidth);
    }

    public void WriteDmResult()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@$"
DM Info:
Scene Complete: {_gameState.LatestDmResultCache.SceneCompletionPercentage}%
current time: {_gameState.LatestDmResultCache.TimeOfDay}
");
    }
}
