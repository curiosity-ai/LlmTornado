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
            Console.WriteLine("Error: OPENAI_API_KEY environment variable not set.");
            Console.WriteLine("Please set your OpenAI API key:");
            Console.Write("API Key: ");
            apiKey = Console.ReadLine();

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Cannot start without API key. Exiting...");
                return;
            }
        }

        _worldState = new FantasyWorldState()
        {
            Player = new FantasyPlayer("John", "High tech space hero destine to become the next king of the galaxy."),
            AdventureTitle = "Echoes of Kestrel-9",
            AdventureFile = "Echoes_of_Kestrel‑9.md",
            MemoryFile = "Echoes_of_Kestrel‑9_progress.md",
            CompletedObjectivesFile = "Echoes_of_Kestrel‑9_completed.md"

        };

        _client = new TornadoApi([
            new ProviderAuthentication(Code.LLmProviders.OpenAi, Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            new ProviderAuthentication(Code.LLmProviders.Google, Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
        ]);


        await Run();

        return;
    }

    static async Task Run()
    {
        ChatRuntime.ChatRuntime runtime = new ChatRuntime.ChatRuntime(new FantasyEngineConfiguration(_client, _worldState));

        await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, "Start Game"));
    }

}