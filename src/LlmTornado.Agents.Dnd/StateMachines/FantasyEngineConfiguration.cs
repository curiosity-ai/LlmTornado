using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;


namespace LlmTornado.Agents.Dnd.FantasyEngine;

internal class FantasyEngineConfiguration : OrchestrationRuntimeConfiguration
{
    private TornadoApi? _client;
    private FantasyWorldState _worldState;

    public FantasyEngineConfiguration(TornadoApi client, FantasyWorldState worldState)
    {
        _client = client;
        _worldState = worldState;

        GameStartRunnable gameStartRunnable = new GameStartRunnable(this);
        DMRunnable narrator = new DMRunnable(_worldState, _client!, this) { AllowsParallelAdvances = true };
        MemoryRunnable memoryUpdatorRunnable = new MemoryRunnable(_client!, _worldState, this) { AllowDeadEnd = true };
        PlayerTurnRunnable playerTurnRunnable = new PlayerTurnRunnable(_client,_worldState, this) { AllowDeadEnd = true };
        DMValidateRunnable actionValidatorRunnable = new DMValidateRunnable(_worldState, _client!, this) { AllowsParallelAdvances = true };
        GameEndRunnable gameEndRunnable = new GameEndRunnable(this) { AllowDeadEnd = true };

        gameStartRunnable.AddAdvancer(narrator);

        narrator.AddAdvancer(memoryUpdatorRunnable);
        narrator.AddAdvancer(playerTurnRunnable);

        actionValidatorRunnable.AddAdvancer((condition) => condition.Result.AllowAction, (converter)=> converter.UserAction, narrator);
        actionValidatorRunnable.AddAdvancer((condition) => !condition.Result.AllowAction, (converter) => new FantasyDMResult() { Narration = converter.Result.Reason }, playerTurnRunnable);


        playerTurnRunnable.AddAdvancer((condition) => !IsPlayerQuitting(condition), actionValidatorRunnable);
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
