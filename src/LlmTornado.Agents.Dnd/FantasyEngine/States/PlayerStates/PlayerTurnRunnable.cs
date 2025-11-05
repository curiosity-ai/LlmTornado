using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Dnd.Agents.Runnables;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.PlayerStates;

internal class PlayerTurnRunnable : OrchestrationRunnable<ChatMessage, PlayerAction>
{
    private readonly FantasyWorldState _gameState;

    public PlayerTurnRunnable(FantasyWorldState state, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _gameState = state;
    }

    public override ValueTask<PlayerAction> Invoke(RunnableProcess<ChatMessage, PlayerAction> input)
    {
        // Get player action
        Dnd.DataModels.GameAction action = GetPlayerAction(_gameState.Player);
        return ValueTask.FromResult(new PlayerAction
        {
            ActionType = action.Type.ToString(),
            Target = action.Target ?? "",
            Description = action.Description,
            Parameters = action.Parameters.Select(kv => new ActionParameter
            {
                Name = kv.Key,
                Value = kv.Value
            }).ToArray()
        });
    }

    /// <summary>
    /// Gets action from human player
    /// </summary>
    private Dnd.DataModels.GameAction GetPlayerAction(FantasyPlayer player)
    {
        Console.WriteLine($"{player.Name}, what do you do?");
        Console.WriteLine(ActionParser.GetAvailableCommands(GamePhase.Adventuring));
        Console.Write("> ");

        string? input = Console.ReadLine();
        return ActionParser.ParseAction(input ?? "", player.Name, GamePhase.Adventuring);
    }

    /// <summary>
    /// Handles special actions like quit, inventory, status
    /// </summary>
    private bool HandleSpecialAction(Dnd.DataModels.GameAction action, FantasyPlayer player)
    {
        if (action.Type == ActionType.Quit)
        {
            return false; // Signal to exit
        }

        if (action.Type == ActionType.ViewInventory)
        {
            ShowInventory(player);
            return true;
        }

        if (action.Type == ActionType.ViewStatus)
        {
            ShowStatus(player);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Shows player inventory
    /// </summary>
    private void ShowInventory(FantasyPlayer player)
    {
        Console.WriteLine($"\n💼 Inventory: {string.Join(", ", player.Inventory.Select(item=>$"{item.Name} -  {item.Description}"))}");
    }

    /// <summary>
    /// Shows player status
    /// </summary>
    private void ShowStatus(FantasyPlayer player)
    {
        Console.WriteLine($"\n📊 {player.Name}");
        Console.WriteLine($"   Backstory: {player.BackStory}");
    }

    /// <summary>
    /// Handles player movement between locations
    /// </summary>
    private bool HandleMovementAsync(Dnd.DataModels.GameAction action, PlayerCharacter player, Location currentLocation)
    {
        if (action.Type != ActionType.Move || string.IsNullOrEmpty(action.Target))
        {
            return false;
        }

        var targetLocation = currentLocation.Exits.FirstOrDefault(e =>
            e.ToLower().Contains(action.Target.ToLower()));

        if (targetLocation != null)
        {
            _gameState.CurrentLocationName = targetLocation;
            _gameState.GameHistory.Add($"Turn {++_gameState.TurnNumber}: Moved to {targetLocation}");

            Console.WriteLine($"\n✅ Moving to {targetLocation}...\n");
            return true;
        }

        Console.WriteLine($"\n❌ Can't go to '{action.Target}' from here.\n");
        return true;
    }


}
