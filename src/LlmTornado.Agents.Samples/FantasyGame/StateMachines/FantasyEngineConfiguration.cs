using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;
using LlmTornado.Agents.Dnd.Game;


namespace LlmTornado.Agents.Dnd.FantasyEngine;

internal class FantasyEngineConfiguration : OrchestrationRuntimeConfiguration
{
    private TornadoApi? _client { get; set; }

    public static FantasyWorldState WorldState = new FantasyWorldState();
    public static UserSettings Settings { get; set; } = new();
    public static string GeneratedAdventuresFilePath => Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "GeneratedAdventures");
    public static string SavedGamesFilePath => Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "SavedGames");
    public static string SettingsFilePath => Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "settings.json");
    public FantasyEngineConfiguration(TornadoApi client)
    {
        _client = client;

        GameStartRunnable gameStartRunnable = new GameStartRunnable(WorldState, this);
        DMRunnable narrator = new DMRunnable(WorldState, _client!, this) { AllowsParallelAdvances = true };
        MemoryRunnable memoryUpdatorRunnable = new MemoryRunnable(_client!, WorldState, this) { AllowDeadEnd = true };
        PlayerTurnRunnable playerTurnRunnable = new PlayerTurnRunnable(_client,WorldState, this) { AllowDeadEnd = true };
        GameEndRunnable gameEndRunnable = new GameEndRunnable(this) { AllowDeadEnd = true };

        gameStartRunnable.AddAdvancer(narrator);

        narrator.AddAdvancer(memoryUpdatorRunnable);
        narrator.AddAdvancer(playerTurnRunnable);

        playerTurnRunnable.AddAdvancer((condition) => !IsPlayerQuitting(condition), narrator);
        playerTurnRunnable.AddAdvancer((condition) => IsPlayerQuitting(condition), gameEndRunnable);

        SetEntryRunnable(gameStartRunnable);
        SetRunnableWithResult(gameEndRunnable);
    }

    public bool IsPlayerQuitting(string input)
    {
        var lowered = input.ToLower();
        return lowered == "/quit" || lowered == "/q" || lowered == "/exit" || lowered == "/end" || lowered == "/e";
    }
}
