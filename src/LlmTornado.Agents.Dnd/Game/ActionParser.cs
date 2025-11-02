using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Parses player input into game actions
/// </summary>
public static class ActionParser
{
    public static GameAction ParseAction(string input, string playerName, GamePhase currentPhase)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new GameAction
            {
                Type = ActionType.ViewStatus,
                PlayerName = playerName,
                Description = "No action"
            };
        }

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var target = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : null;

        return command switch
        {
            // General actions
            "inventory" or "inv" => new GameAction
            {
                Type = ActionType.ViewInventory,
                PlayerName = playerName,
                Description = "View inventory"
            },
            "status" or "stats" => new GameAction
            {
                Type = ActionType.ViewStatus,
                PlayerName = playerName,
                Description = "View status"
            },
            "quit" or "exit" => new GameAction
            {
                Type = ActionType.Quit,
                PlayerName = playerName,
                Description = "Quit game"
            },

            // Adventuring phase actions
            "move" or "go" or "travel" when currentPhase == GamePhase.Adventuring => new GameAction
            {
                Type = ActionType.Move,
                Target = target,
                PlayerName = playerName,
                Description = $"Move to {target}"
            },
            "talk" or "speak" when currentPhase == GamePhase.Adventuring => new GameAction
            {
                Type = ActionType.Talk,
                Target = target,
                PlayerName = playerName,
                Description = $"Talk to {target}"
            },
            "examine" or "look" when currentPhase == GamePhase.Adventuring => new GameAction
            {
                Type = ActionType.Examine,
                Target = target,
                PlayerName = playerName,
                Description = $"Examine {target ?? "surroundings"}"
            },
            "search" when currentPhase == GamePhase.Adventuring => new GameAction
            {
                Type = ActionType.Search,
                PlayerName = playerName,
                Description = "Search the area"
            },
            "rest" when currentPhase == GamePhase.Adventuring => new GameAction
            {
                Type = ActionType.Rest,
                PlayerName = playerName,
                Description = "Rest and recover"
            },
            "shop" or "trade" when currentPhase == GamePhase.Adventuring => new GameAction
            {
                Type = ActionType.EnterShop,
                PlayerName = playerName,
                Description = "Enter shop"
            },

            // Combat phase actions
            "attack" or "fight" when currentPhase == GamePhase.Combat => new GameAction
            {
                Type = ActionType.Attack,
                Target = target,
                PlayerName = playerName,
                Description = $"Attack {target}"
            },
            "move" when currentPhase == GamePhase.Combat && parts.Length >= 3 => new GameAction
            {
                Type = ActionType.CombatMove,
                PlayerName = playerName,
                Description = $"Move to position",
                Parameters = new Dictionary<string, string>
                {
                    { "x", parts[1] },
                    { "y", parts[2] }
                }
            },
            "use" when currentPhase == GamePhase.Combat => new GameAction
            {
                Type = ActionType.UseItem,
                Target = target,
                PlayerName = playerName,
                Description = $"Use {target}"
            },
            "defend" or "block" when currentPhase == GamePhase.Combat => new GameAction
            {
                Type = ActionType.Defend,
                PlayerName = playerName,
                Description = "Take defensive stance"
            },
            "retreat" or "flee" or "run" when currentPhase == GamePhase.Combat => new GameAction
            {
                Type = ActionType.Retreat,
                PlayerName = playerName,
                Description = "Attempt to retreat"
            },

            // Shopping phase actions
            "buy" when currentPhase == GamePhase.Shopping => new GameAction
            {
                Type = ActionType.Buy,
                Target = target,
                PlayerName = playerName,
                Description = $"Buy {target}"
            },
            "sell" when currentPhase == GamePhase.Shopping => new GameAction
            {
                Type = ActionType.Sell,
                Target = target,
                PlayerName = playerName,
                Description = $"Sell {target}"
            },
            "leave" or "exit" when currentPhase == GamePhase.Shopping => new GameAction
            {
                Type = ActionType.ExitShop,
                PlayerName = playerName,
                Description = "Exit shop"
            },

            // Default
            _ => new GameAction
            {
                Type = currentPhase == GamePhase.Combat ? ActionType.Defend : ActionType.Examine,
                Target = target,
                PlayerName = playerName,
                Description = $"Unknown command: {command}"
            }
        };
    }

    public static string GetAvailableCommands(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.Adventuring => 
                "Commands: [move/go] [location], [talk] [npc], [examine] [object], [search], [rest], [shop], [inventory], [status], [quit]",
            
            GamePhase.Combat => 
                "Combat Commands: [attack] [target], [move] [x] [y], [use] [item], [defend], [retreat], [inventory], [status]",
            
            GamePhase.Shopping => 
                "Shop Commands: [buy] [item], [sell] [item], [leave], [inventory], [status]",
            
            _ => "Commands: [inventory], [status], [quit]"
        };
    }
}
