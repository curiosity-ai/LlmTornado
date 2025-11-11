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
using System;
using System.Text;
using ChatRuntimeClass = LlmTornado.Agents.ChatRuntime.ChatRuntime;

namespace LlmTornado.Agents.Dnd;

class Program
{
    private static TornadoApi? _client;
    private static FantasyWorldState _worldState;
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

        _worldState = CreateWorldState("Shadows_in_Ironkeep__A_Prison_Break_Heist.json");

        _client = new TornadoApi([
            new ProviderAuthentication(Code.LLmProviders.OpenAi, Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            new ProviderAuthentication(Code.LLmProviders.Google, Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
        ]);

        await Run(); // GenerateNewAdventure();

        SaveWorldState(_worldState);

        return;
    }

    static async Task Run()
    {
        ChatRuntime.ChatRuntime runtime = new ChatRuntime.ChatRuntime(new FantasyEngineConfiguration(_client, _worldState));

        await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, "start new game"));
    }

    public static async Task GenerateNewAdventure()
    {
        var generator = new AdventureMdGeneratorRunnable(_client, new Orchestration<bool,bool>());
        await generator.Invoke(new RunnableProcess<string, bool>(generator, "Prison Break adventure", "123"));
        Console.WriteLine("Adventure generated.");
    }
    public static FantasyWorldState CreateWorldState(string adventureFile)
    {
        FantasyWorldState worldState = new FantasyWorldState()
        {
            AdventureFile = adventureFile
        };
        worldState.Adventure = new();
        worldState.Adventure.DeserializeFromFile(worldState.AdventureFile);
        worldState.AdventureTitle = worldState.Adventure.Title;
        worldState.MemoryFile = $"{worldState.AdventureTitle.Replace(" ", "_").Replace(":","_")}_Memory.md";
        worldState.CurrentLocationName = worldState.Adventure.Locations.Where(l => l.Id == worldState.Adventure.PlayerStartingInfo.StartingLocationId).FirstOrDefault().Name ?? "Unknown";

        return worldState;
    }
    public static void SaveWorldState(FantasyWorldState worldState)
    {
        worldState.SerializeToFile(worldState.AdventureFile.Replace(".json", "_State.json"));
    }

    public static FantasyWorldState LoadWorldState(string filePath)
    {
        return FantasyWorldState.DeserializeFromFile(filePath);
    }

}