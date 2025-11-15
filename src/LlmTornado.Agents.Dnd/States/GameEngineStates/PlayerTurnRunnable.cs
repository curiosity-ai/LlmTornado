using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Dnd.Agents.Runnables;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
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
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;

internal class PlayerTurnRunnable : OrchestrationRunnable<FantasyDMResult, string>
{
    private readonly FantasyWorldState _gameState;
    private readonly TornadoApi _client;
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
        Console.ForegroundColor = ConsoleColor.White;
        
        Console.Write("\n[Dungeon Master]:\n\n");
        WrapText(input.Input.Narration, maxConsoleWidth);
        Console.Out.Flush(); // Force the buffered output to be displayed immediately
        Console.Write("\nAvailable Actions:\n");
        foreach (var action in input.Input.NextActions)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            WrapText($"- {action.Action}", maxConsoleWidth);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("     Success Rate:");
            WrapText($"{action.MinimumSuccessThreshold}", maxConsoleWidth);
            Console.Write($"     Duration:{action.DurationHours}Hrs");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("     Success Outcome: ");
            WrapText($"{action.SuccessOutcome}", maxConsoleWidth);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("     Failure Outcome: ");
            WrapText($"{action.FailureOutcome}", maxConsoleWidth);
        }
        Console.Out.Flush(); // Force the buffered output to be displayed immediately
        Console.ForegroundColor = ConsoleColor.Yellow;
        await TTS(input.Input.Narration);
        string? result = PlayerInputLoop(dMResult);


        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" \n  ");
        return result;
    }

    public string PlayerInputLoop(FantasyDMResult dMResult)
    {
        string? result = "";
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\nAdditional Commands: /info, /dm, /help, /rest");
            Console.Write($"\n[Player]:");
            Console.Out.Flush(); // Force the buffered output to be displayed immediately
            result = Console.ReadLine();
            if (result.ToLower() == "/h" || result.ToLower() == "/help")
            {
                WriteHelp();
            }
            else if (result.ToLower() == "/i" || result.ToLower() == "/info")
            {
                WriteWorldInfo();
            }
            else if (result.ToLower() == "/d" || result.ToLower() == "/dm")
            {
                WriteDmResult(dMResult);
            }
            else if(result.ToLower() == "/rest" || result.ToLower() == "/r")
            {
                Console.WriteLine($"Time: day {_gameState.CurrentDay} - HR: [{_gameState.CurrentTimeOfDay}] ");
                Console.WriteLine("Resting... ");
                _gameState.Rest();
                Console.WriteLine($"Time: day {_gameState.CurrentDay} - HR: [{_gameState.CurrentTimeOfDay}] ");
                result = "Player chooses to rest.";
                break;

            }
            else if (string.IsNullOrEmpty(result))
            {
                Console.WriteLine("Input cannot be empty please type ... if you dont want to respond.");
            }
            else
            {
                break;
            }
        }

        return result;
    }

    public async Task TTS(string text)
    {
        // Placeholder for TTS functionality
        // In a real implementation, this would call a TTS service to read the text aloud
        SpeechTtsResult? result = await _client.Audio.CreateSpeech(new SpeechRequest
        {
            Input = "Hi, how are you?",
            Model = AudioModel.OpenAi.Gpt4.Gpt4OMiniTts,
            ResponseFormat = SpeechResponseFormat.Mp3,
            Voice = SpeechVoice.Alloy,
            Instructions = "You are a enthusiastic Dungeon Master. Try to change your voice for different actors in the scene you are narrating."
        });

        if (result is not null)
        {
            await result.SaveAndDispose("ttsdemo.mp3");
        }

        TimeSpan duration = GetWavFileDuration("ttsdemo.mp3");

        using (var audioFile = new AudioFileReader("ttsdemo.mp3"))
            
        using (var outputDevice = new WaveOutEvent())
        {
            outputDevice.Init(audioFile);
            outputDevice.Play();

            while (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                Thread.Sleep(duration.Milliseconds + 500);
            }
        }
    }
    public static TimeSpan GetWavFileDuration(string fileName)
    {
        using (var reader = new WaveFileReader(fileName))
        {
            return reader.TotalTime;
        }
    }

    public void WriteHelp()
    {
        Console.WriteLine("/quit or /exit to exit the game.");
        Console.WriteLine("/info or /i get get world state information");
        Console.WriteLine("/dm or /d to get dm information");
        Console.WriteLine("/help or /h to get this help menu again");
        Console.WriteLine("/rest or /r to rest");
    }

    public void WriteWorldInfo()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        WrapText(@$"

World Info:
# {_gameState.Adventure.Title} 

- Act {_gameState.CurrentAct + 1}: {_gameState.Adventure.Acts[_gameState.CurrentAct].Title} 

- Scene {_gameState.CurrentScene + 1}: {_gameState.Adventure.Acts[_gameState.CurrentAct].Scenes[_gameState.CurrentScene]}

- Location {_gameState.CurrentLocation.ToString()}

- Scene Turn {_gameState.CurrentSceneTurns}

- Current Time: Day  {_gameState.CurrentDay} : Hr {_gameState.CurrentTimeOfDay}

", maxConsoleWidth);
    }

    public void WriteDmResult(FantasyDMResult dMResult)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@$"
DM Info:
Scene Complete: {dMResult.SceneCompletionPercentage}%
current time: {dMResult.TimeOfDay}
");
    }

    public void WrapText(string text, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string[] words = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder currentLine = new StringBuilder();

        foreach (string word in words)
        {
            // If adding this word would exceed maxWidth
            if (currentLine.Length > 0 && currentLine.Length + 1 + word.Length > maxWidth)
            {
                // Output current line and start fresh
                Console.WriteLine(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(word);
            }
            else
            {
                // Add word to current line
                if (currentLine.Length > 0)
                {
                    currentLine.Append(" ");
                }
                currentLine.Append(word);
            }
        }

        // Output any remaining text
        if (currentLine.Length > 0)
        {
            Console.WriteLine(currentLine.ToString());
        }
    }
}
