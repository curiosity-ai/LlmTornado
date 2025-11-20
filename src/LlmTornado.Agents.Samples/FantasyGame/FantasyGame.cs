using LlmTornado.Agents.Dnd.FantasyEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Samples.FantasyGame;

public class FantasyGame
{
    public static async Task RunGame(TornadoApi client)
    {
        ChatRuntime.ChatRuntime runtime = new ChatRuntime.ChatRuntime(new FantasyMainMenuConfiguration(client));

        await runtime.InvokeAsync(new Chat.ChatMessage(Code.ChatMessageRoles.User, "Start New Game"));
    }
}
