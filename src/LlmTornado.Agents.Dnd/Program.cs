using LlmTornado;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.Agents;
using LlmTornado.Agents.Dnd.FantasyEngine;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat.Models;
using System.Text;

namespace LlmTornado.Agents.Dnd;

class Program
{

    static async Task Main(string[] args)
    {
        await Run(); 

        return;
    }

    static async Task Run()
    {
        TornadoApi _client = new TornadoApi(Code.LLmProviders.OpenAi, Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

        ChatRuntime.ChatRuntime runtime = new ChatRuntime.ChatRuntime(new FantasyMainMenuConfiguration(_client));

        await runtime.InvokeAsync(new Chat.ChatMessage(Code.ChatMessageRoles.User, "Start New Game"));
    }
}