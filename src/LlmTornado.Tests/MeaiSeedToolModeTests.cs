using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class MeaiSeedToolModeTests
{
    private const string ReplyJson = """
        {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"gpt-4o",
         "choices":[{"index":0,"message":{"role":"assistant","content":"{\"city\":\"Paris\"}"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":5,"completion_tokens":4,"total_tokens":9}}
        """;

    [Test]
    public async Task ChatClient_Seed_IsSent()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { Seed = 1234 });

        Assert.That(JObject.Parse(server.RequestBodies.Single())["seed"]?.Value<int>(), Is.EqualTo(1234));
    }

    [Test]
    public async Task ChatClient_ToolModeNone_SendsToolChoiceNone()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), WithTool(o => o.ToolMode = ChatToolMode.None));

        Assert.That(JObject.Parse(server.RequestBodies.Single())["tool_choice"]?.Value<string>(), Is.EqualTo("none"));
    }

    [Test]
    public async Task ChatClient_ToolModeRequireAny_SendsToolChoiceRequired()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), WithTool(o => o.ToolMode = ChatToolMode.RequireAny));

        Assert.That(JObject.Parse(server.RequestBodies.Single())["tool_choice"]?.Value<string>(), Is.EqualTo("required"));
    }

    [Test]
    public async Task ChatClient_ToolModeRequireSpecific_SendsTheFunction()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), WithTool(o => o.ToolMode = ChatToolMode.RequireSpecific("get_weather")));

        JToken? choice = JObject.Parse(server.RequestBodies.Single())["tool_choice"];
        Assert.That(choice?["function"]?["name"]?.Value<string>(), Is.EqualTo("get_weather"));
    }

    [Test]
    public async Task ChatClient_AllowMultipleToolCallsFalse_DisablesParallelToolCalls()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), WithTool(o => o.AllowMultipleToolCalls = false));

        Assert.That(JObject.Parse(server.RequestBodies.Single())["parallel_tool_calls"]?.Value<bool>(), Is.False);
    }

    [Test]
    public async Task ChatClient_ToolModeWithoutTools_SendsNoToolChoice()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { ToolMode = ChatToolMode.RequireAny, AllowMultipleToolCalls = false });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["tool_choice"], Is.Null);
        Assert.That(body["parallel_tool_calls"], Is.Null);
    }

    private static IChatClient CreateClient(int port)
    {
        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{port}/v1/{{0}}{{1}}"
        });

        return api.AsChatClient(ChatModel.OpenAi.Gpt4.O);
    }

    private static ChatOptions WithTool(Action<ChatOptions> configure)
    {
        ChatOptions options = new ChatOptions
        {
            Tools = [AIFunctionFactory.Create((string city) => $"{city}: 18 C", "get_weather", "Gets the weather for a city.")]
        };
        configure(options);
        return options;
    }

    private static List<global::Microsoft.Extensions.AI.ChatMessage> Prompt() =>
    [
        new global::Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "Weather in Paris, as JSON.")
    ];

    private sealed class FakeOpenAiServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string response;

        public int Port { get; }
        public List<string> RequestBodies { get; } = [];

        public FakeOpenAiServer(string response)
        {
            this.response = response;

            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            Port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            listener.Start();
            _ = Serve();
        }

        private async Task Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // listener closed
                    return;
                }

                using (StreamReader reader = new StreamReader(context.Request.InputStream))
                {
                    RequestBodies.Add(await reader.ReadToEndAsync().ConfigureAwait(false));
                }

                byte[] bytes = Encoding.UTF8.GetBytes(response);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            listener.Close();
        }
    }
}
