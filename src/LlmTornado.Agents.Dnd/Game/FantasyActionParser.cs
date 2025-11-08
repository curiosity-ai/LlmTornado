using LlmTornado.Agents.Dnd.DataModels;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Parses player input into game actions
/// </summary>
public static class FantasyActionParser
{
    public static PlayerAction ParseAction(string input, string playerName)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new PlayerAction
            {
                ActionType = "Talk",
                Target = playerName,
                Description = "No action"
            };
        }

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var target = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : null;

        return command switch
        {
            // General actions
            "inventory" or "inv" => new PlayerAction
            {
                ActionType = command,
                Target = target,
                Description = $"Parts: {parts}"
            },
            // Default
            _ => new PlayerAction
            {
                ActionType = "Unknown",
                Target = target,
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
                "Combat Commands: [attack] [target], [use] [item], [defend], [inventory], [status]",
            
            GamePhase.Shopping => 
                "Shop Commands: [buy] [item], [sell] [item], [leave], [inventory], [status]",
            
            _ => "Commands: [inventory], [status], [quit]"
        };
    }
}
