using LlmTornado;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.Agents;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Agents.Dnd.Utility;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Mcp;
using Newtonsoft.Json;
using System;
using System.Text;

using ChatRuntimeClass = LlmTornado.Agents.ChatRuntime.ChatRuntime;

namespace LlmTornado.Agents.Dnd;

class Program
{
    private const string DisableTtsEnvVar = "LLMTORNADO_DND_DISABLE_TTS";
    private static TornadoApi? _client;
    public static FantasyWorldState WorldState = new();
    public static UserSettings Settings { get; private set; } = new();
    public static string GeneratedAdventuresFilePath => Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "GeneratedAdventures");
    public static string SavedGamesFilePath => Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "SavedGames");
    public static string SettingsFilePath => Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "settings.json");

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;

        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        LlmTornado D&D - AI-Powered Dungeon & Dragons Adventure         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.Out.Flush(); // Force the buffered output to be displayed immediately

        CheckFoldersExist();

        Settings = UserSettings.Load(SettingsFilePath);
        WorldState.EnableTts = ResolveTtsPreference(args, Settings.EnableTts);
        Settings.EnableTts = WorldState.EnableTts;
        Settings.Save(SettingsFilePath);

        Console.WriteLine(WorldState.EnableTts
            ? "[TTS enabled. Use Main Menu > Settings or --no-tts / LLMTORNADO_DND_DISABLE_TTS=1 to mute narration audio.]"
            : "[TTS disabled. Use Main Menu > Settings, --tts, or clear LLMTORNADO_DND_DISABLE_TTS to re-enable narration audio.]");

        // Initialize API client
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: OPENAI_API_KEY environment variable not set.\nPlease Open Computer environment variables in settings and add OPENAI_API_KEY to system variables\n" +
                "Then restart your computer or Enter API key now.");
            Console.WriteLine("Please set your OpenAI API key:");
            Console.Write("API Key: ");
            apiKey = Console.ReadLine();

            if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
            {
                Console.WriteLine("Cannot start without API key. Exiting...");
                return;
            }
        }

        _client = new TornadoApi([
            new ProviderAuthentication(Code.LLmProviders.OpenAi, apiKey)
        ]);

        await Run(); 

        return;
    }

    static async Task Run()
    {
        ChatRuntime.ChatRuntime runtime = new ChatRuntime.ChatRuntime(new FantasyMainMenuConfiguration(_client, WorldState));

        await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, "Set the scene"));
    }

    static void CheckFoldersExist()
    {
        string saveDataDir = Path.Combine(Directory.GetCurrentDirectory(), "Game_Data");
        if (!Directory.Exists(saveDataDir))
        {
            Directory.CreateDirectory(saveDataDir);
        }
        string adventuresDir = Path.Combine(saveDataDir, "GeneratedAdventures");
        if (!Directory.Exists(adventuresDir))
        {
            Directory.CreateDirectory(adventuresDir);
        }
        string savesDir = Path.Combine(saveDataDir, "SavedGames");
        if (!Directory.Exists(savesDir))
        {
            Directory.CreateDirectory(savesDir);
        }
    }

    static bool ResolveTtsPreference(string[] args, bool defaultValue)
    {
        bool enableTts = defaultValue;

        var disableEnvValue = Environment.GetEnvironmentVariable(DisableTtsEnvVar);
        if (TryParseBooleanFlag(disableEnvValue, out bool disableEnvFlag) && disableEnvFlag)
        {
            enableTts = false;
        }

        foreach (var arg in args)
        {
            if (arg.Equals("--no-tts", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-no-tts", StringComparison.OrdinalIgnoreCase))
            {
                enableTts = false;
            }
            else if (arg.Equals("--tts", StringComparison.OrdinalIgnoreCase))
            {
                enableTts = true;
            }
            else if (arg.StartsWith("--tts=", StringComparison.OrdinalIgnoreCase))
            {
                var valuePart = arg.Substring(arg.IndexOf('=') + 1);
                if (TryParseBooleanFlag(valuePart, out bool parsedValue))
                {
                    enableTts = parsedValue;
                }
            }
        }

        return enableTts;
    }

    static bool TryParseBooleanFlag(string? value, out bool parsed)
    {
        parsed = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out parsed))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "y":
            case "on":
                parsed = true;
                return true;
            case "0":
            case "no":
            case "n":
            case "off":
                parsed = false;
                return true;
            default:
                return false;
        }
    }
}