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
public class MeaiStrictTests
{
    private const string ReplyJson = """
        {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"gpt-4o",
         "choices":[{"index":0,"message":{"role":"assistant","content":"{\"city\":\"Paris\"}"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":5,"completion_tokens":4,"total_tokens":9}}
        """;

    private const string Schema = """{"type":"object","properties":{"city":{"type":"string"}},"required":["city"],"additionalProperties":false}""";

    [Test]
    public async Task ChatClient_OptionsStrict_AppliesToEveryTool()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { Tools = [Weather()], AdditionalProperties = new AdditionalPropertiesDictionary { ["strict"] = true } });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["tools"]?[0]?["strict"]?.Value<bool>(), Is.True);
    }

    [Test]
    public async Task ChatClient_ToolStrict_WinsOverOptionsStrict()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { Tools = [Weather(strict: false)], AdditionalProperties = new AdditionalPropertiesDictionary { ["strict"] = true } });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["tools"]?[0]?["strict"]?.Value<bool>(), Is.False);
    }

    [Test]
    public async Task ChatClient_ToolStrict_AloneIsSent()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { Tools = [Weather(strict: true)] });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["tools"]?[0]?["strict"]?.Value<bool>(), Is.True);
    }

    [Test]
    public async Task ChatClient_NoStrict_LeavesToolsUnchanged()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { Tools = [Weather()] });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["tools"]?[0]?["strict"], Is.Null);
    }

    [Test]
    public async Task ChatClient_OptionsStrict_AppliesToResponseSchema()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(System.Text.Json.JsonDocument.Parse(Schema).RootElement, "weather_report"),
            AdditionalProperties = new AdditionalPropertiesDictionary { ["strict"] = true }
        });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["response_format"]?["json_schema"]?["strict"]?.Value<bool>(), Is.True);
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

    private static AIFunction Weather(bool? strict = null) =>
        AIFunctionFactory.Create((string city) => $"{city}: 18 C", new AIFunctionFactoryOptions
        {
            Name = "get_weather",
            Description = "Gets the weather for a city.",
            AdditionalProperties = strict is null ? null : new Dictionary<string, object?> { ["strict"] = strict.Value }
        });

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
