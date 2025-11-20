using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Dnd.Agents.Runnables;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat;
using LlmTornado.Code;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;

internal class GameStartRunnable : OrchestrationRunnable<ChatMessage, string>
{
    private TornadoApi? _client;
    private FantasyWorldState _worldState;

    public GameStartRunnable(FantasyWorldState gameState, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _worldState = gameState;
    }

    public override ValueTask<string> Invoke(RunnableProcess<ChatMessage, string> input)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
        Console.Clear();

        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        LlmTornado D&D - AI-Powered Dungeon & Dragons Adventure         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.Out.Flush(); // Force the buffered output to be displayed immediately

        CheckFoldersExist();

        //Set settings here
        UserSettings Settings = UserSettings.Load(FantasyEngineConfiguration.SettingsFilePath);
        _worldState.EnableTts = Settings.EnableTts;

        return ValueTask.FromResult(
            "Set the scene"
        );
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
}
