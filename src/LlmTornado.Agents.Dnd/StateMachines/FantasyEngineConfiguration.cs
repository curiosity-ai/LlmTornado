using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;
using LlmTornado.Agents.Dnd.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

internal class FantasyEngineConfiguration : OrchestrationRuntimeConfiguration
{
    private static FantasyWorldPersistence? _persistence;
    private static TornadoApi? _client;
    private static FantasyWorldState _worldState;

    public FantasyEngineConfiguration(TornadoApi client, FantasyWorldState worldState)
    {
        _client = client;
        _worldState = worldState;
        //_persistence = new FantasyWorldPersistence($"{_worldState.AdventureTitle.Replace(" ", "_")}_ChatHistory.json");

        GameStartRunnable gameStartRunnable = new GameStartRunnable(this);
        DMRunnable narrator = new DMRunnable(_worldState, _client!, this) { AllowsParallelAdvances = true };
        MarkdownMemoryUpdatorRunnable memoryUpdatorRunnable = new MarkdownMemoryUpdatorRunnable(_client!, _worldState, this) { AllowDeadEnd = true };
        PlayerTurnRunnable playerTurnRunnable = new PlayerTurnRunnable(_worldState, this) { AllowDeadEnd = true };
        GameEndRunnable gameEndRunnable = new GameEndRunnable(this) { AllowDeadEnd = true };

        gameStartRunnable.AddAdvancer(narrator);

        narrator.AddAdvancer(memoryUpdatorRunnable);
        narrator.AddAdvancer(playerTurnRunnable);

        playerTurnRunnable.AddAdvancer((result) => result.ToLower() != "quit",narrator);
        playerTurnRunnable.AddAdvancer((result) => result.ToLower() == "quit", gameEndRunnable);

        SetEntryRunnable(gameStartRunnable);
        SetRunnableWithResult(gameEndRunnable);
    }
}
