using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.Agents.FantasyEngine;
using LlmTornado.Agents.Dnd.Agents.Runnables.FantasyEngine;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.MainMenuState;
using LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;
using LlmTornado.Chat;
using Microsoft.Extensions.Options;
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
        GenerateNewAdventureRunnable GenerateAdventureState = new GenerateNewAdventureRunnable(_client, this);
        EditAdventureRunnable EditAdventureState = new EditAdventureRunnable(_client, this);
        DeleteAdventureRunnable DeleteAdventureState = new DeleteAdventureRunnable(this);
        DeleteSaveFileRunnable DeleteSaveFileState = new DeleteSaveFileRunnable(this);
        SettingsRunnable SettingsState = new SettingsRunnable(this);
        RunGameRunnable RunGameState = new RunGameRunnable(_client, this);
        QuitGameRunnable QuitGameState = new QuitGameRunnable(this) { AllowDeadEnd = true };

        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.StartNewAdventure, StartNewGameState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.LoadSavedGame, LoadGameState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.GenerateNewAdventure, GenerateAdventureState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.EditGeneratedAdventure, EditAdventureState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.DeleteAdventure, DeleteAdventureState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.DeleteSaveFile, DeleteSaveFileState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.Settings, SettingsState);
        MainMenuState.AddAdvancer(sel => sel == MainMenuSelection.QuitGame, QuitGameState);

        GenerateAdventureState.AddAdvancer(condition=>true,conversion=>new ChatMessage(Code.ChatMessageRoles.User, "generated") ,MainMenuState);
        DeleteAdventureState.AddAdvancer(condition => true, conversion => new ChatMessage(Code.ChatMessageRoles.User, "deleted"), MainMenuState);
        EditAdventureState.AddAdvancer(condition => true, conversion => new ChatMessage(Code.ChatMessageRoles.User, "edited"), MainMenuState);
        DeleteSaveFileState.AddAdvancer(condition => true, conversion => new ChatMessage(Code.ChatMessageRoles.User, "deleted"), MainMenuState);
        SettingsState.AddAdvancer(condition => true, conversion => new ChatMessage(Code.ChatMessageRoles.User, "updated"), MainMenuState);
        StartNewGameState.AddAdvancer(condition => condition == false, conversion => new ChatMessage(Code.ChatMessageRoles.User, "generated"), MainMenuState);
        LoadGameState.AddAdvancer(condition => condition == false, conversion => new ChatMessage(Code.ChatMessageRoles.User, "generated"), MainMenuState);
        StartNewGameState.AddAdvancer(condition => condition == true, conversion => "New Game", RunGameState);
        LoadGameState.AddAdvancer(condition => condition == true, conversion => "Set the scene briefly, summarizing recent events.", RunGameState);

        RunGameState.AddAdvancer(MainMenuState);

        SetEntryRunnable(MainMenuState);
        SetRunnableWithResult(QuitGameState);
    }
}
