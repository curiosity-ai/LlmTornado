using LlmTornado;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.Agents;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Agents.Dnd.Persistence;
using LlmTornado.Agents.Dnd.States.GeneratorStates;
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
    private static TornadoApi? _client;
    private static FantasyWorldState _worldState;

    private static string FirstMessage = "Set the scene";
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        LlmTornado D&D - AI-Powered Adventure                           ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

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

        _worldState = LoadWorldState("Shadows_in_Ironkeep__A_Prison_Break_Heist_State.json");

        long fileLength = new System.IO.FileInfo(_worldState.MemoryFile).Length;

        if (fileLength > 200)
        {
            FirstMessage = "Set the scene briefly, summarizing recent events.";
            Console.WriteLine("loading memory...");
        }
        else
        {
            FirstMessage = "Set the scene.";
            Console.WriteLine("no memory file found, starting fresh...");
        }

        _client = new TornadoApi([
            new ProviderAuthentication(Code.LLmProviders.OpenAi, apiKey)
        ]);

        await Run(); // GenerateNewAdventure();

        SaveWorldState(_worldState);

        return;
    }

    static async Task Run()
    {
        ChatRuntime.ChatRuntime runtime = new ChatRuntime.ChatRuntime(new FantasyEngineConfiguration(_client, _worldState));

        await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, FirstMessage));
    }

    public static async Task GenerateNewAdventure()
    {
        var generator = new AdventureGeneratorRunnable(_client, new Orchestration<bool,bool>());
        await generator.Invoke(new RunnableProcess<string, bool>(generator, "Prison Break adventure", "123"));
        Console.WriteLine("Adventure generated.");
    }


    public static FantasyWorldState CreateWorldState(string adventureFile)
    {
        FantasyWorldState worldState = new FantasyWorldState()
        {
            SaveDataDirectory = adventureFile
        };
        worldState.AdventureResult = new();
        worldState.AdventureResult = worldState.AdventureResult.DeserializeFromFile(worldState.AdventureFile);
        worldState.MemoryFile = $"{worldState.Adventure.Title.Replace(" ", "_").Replace(":","_")}_Memory.md";
        worldState.Adventure= worldState.AdventureResult.ToFantasyAdventure();
        worldState.WorldStateFile = worldState.AdventureFile.Replace(".json", "_State.json");
        if (worldState.CurrentLocation is null)
        {
            worldState.CurrentLocation = worldState.Adventure.Locations.FirstOrDefault(location => worldState.Adventure.PlayerStartingInfo.StartingLocationId == location.Id) ?? new FantasyLocation("Unknown", "Unknown", "unknown");
        }
        Console.WriteLine($"Loaded adventure: {worldState.Adventure.Title}");
        worldState.SerializeToFile(worldState.AdventureFile.Replace(".json", "_State.json"));
        Console.WriteLine($"Created world state file: {worldState.AdventureFile.Replace(".json", "_State.json")}");
        return worldState;
    }

    public static FantasyWorldState LoadWorldStateFromFile(string stateFilePath)
    {
        FantasyWorldState worldState = FantasyWorldState.DeserializeFromFile(stateFilePath);
        Console.WriteLine($"Loaded world state from file: {stateFilePath}");
        return worldState;
    }

    public static void SaveWorldState(FantasyWorldState worldState)
    {
        worldState.SerializeToFile(worldState.AdventureFile.Replace(".json", "_State.json"));
    }

    public static FantasyWorldState LoadWorldState(string filePath)
    {
        var worldState = LoadWorldStateFromFile(filePath);
        worldState.AdventureResult = new();
        worldState.AdventureResult = worldState.AdventureResult.DeserializeFromFile(worldState.AdventureFile);
        worldState.Adventure = worldState.AdventureResult.ToFantasyAdventure();
        return worldState;
    }
}