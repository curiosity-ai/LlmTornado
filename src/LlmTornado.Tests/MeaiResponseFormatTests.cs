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
public class MeaiResponseFormatTests
{
    private const string ReplyJson = """
        {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"gpt-4o",
         "choices":[{"index":0,"message":{"role":"assistant","content":"{\"city\":\"Paris\"}"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":5,"completion_tokens":4,"total_tokens":9}}
        """;

    [Test]
    public async Task ChatClient_JsonSchemaResponseFormat_SendsTheSchema()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        ChatOptions options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                System.Text.Json.JsonDocument.Parse("""{"type":"object","properties":{"city":{"type":"string"}},"required":["city"],"additionalProperties":false}""").RootElement,
                "weather_report",
                "A weather report")
        };

        await client.GetResponseAsync(Prompt(), options);

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["response_format"]?["type"]?.Value<string>(), Is.EqualTo("json_schema"));
        Assert.That(body["response_format"]?["json_schema"]?["name"]?.Value<string>(), Is.EqualTo("weather_report"));
        Assert.That(body["response_format"]?["json_schema"]?["description"]?.Value<string>(), Is.EqualTo("A weather report"));
        Assert.That(body["response_format"]?["json_schema"]?["schema"]?["properties"]?["city"]?["type"]?.Value<string>(), Is.EqualTo("string"));
    }

    [Test]
    public async Task ChatClient_JsonResponseFormat_SendsJsonObject()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { ResponseFormat = ChatResponseFormat.Json });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["response_format"]?["type"]?.Value<string>(), Is.EqualTo("json_object"));
    }

    [Test]
    public async Task ChatClient_TextResponseFormat_SendsNoResponseFormat()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer(ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { ResponseFormat = ChatResponseFormat.Text });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["response_format"], Is.Null);
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
