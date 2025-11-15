using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.Agents.FantasyEngine;
using LlmTornado.Agents.Dnd.Agents.Runnables.FantasyEngine;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.MainMenuState;
using LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;
using LlmTornado.Chat;
using System.Runtime.CompilerServices;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

internal class FantasyMainMenuConfiguration : OrchestrationRuntimeConfiguration
{
    private static TornadoApi? _client;
    private static FantasyWorldState _worldState;

    public FantasyMainMenuConfiguration(TornadoApi client, FantasyWorldState worldState)
    {
        _client = client;
        _worldState = worldState;

        MainMenuRunnable MainMenuState = new MainMenuRunnable(this);

        StartNewGameRunnable StartNewGameState = new StartNewGameRunnable(this);
        LoadGameRunnable LoadGameState = new LoadGameRunnable(this);
        GenerateAdventureRunnable GenerateAdventureState = new GenerateAdventureRunnable(_client, this);
        RunGameRunnable RunGameState = new RunGameRunnable(_worldState, _client, this);
        QuitGameRunnable QuitGameState = new QuitGameRunnable(this) { AllowDeadEnd = true };

        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.StartNewAdventure, StartNewGameState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.LoadSavedGame, LoadGameState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.GenerateNewAdventure, GenerateAdventureState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.QuitGame, QuitGameState);

        GenerateAdventureState.AddAdvancer(condition=>condition == true,conversion=>new ChatMessage(Code.ChatMessageRoles.User, "generated") ,MainMenuState);
        StartNewGameState.AddAdvancer(condition => condition == false, conversion => new ChatMessage(Code.ChatMessageRoles.User, "generated"), MainMenuState);
        LoadGameState.AddAdvancer(condition => condition == false, conversion => new ChatMessage(Code.ChatMessageRoles.User, "generated"), MainMenuState);
        StartNewGameState.AddAdvancer(condition => condition == true, conversion => "Set the scene.", RunGameState);
        LoadGameState.AddAdvancer(condition => condition == true, conversion => "Set the scene briefly, summarizing recent events.", RunGameState);

        RunGameState.AddAdvancer(MainMenuState);

        SetEntryRunnable(MainMenuState);
        SetRunnableWithResult(QuitGameState);
    }
}
