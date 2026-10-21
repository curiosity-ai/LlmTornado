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
public class MeaiReasoningEffortTests
{
    private const string ReplyJson = """
        {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"gpt-4o",
         "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":5,"completion_tokens":1,"total_tokens":6}}
        """;

    private const string StreamSse = """
        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"ok"},"finish_reason":null}]}

        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

        data: [DONE]


        """;

    [TestCase(ReasoningEffort.None, "none")]
    [TestCase(ReasoningEffort.Low, "low")]
    [TestCase(ReasoningEffort.Medium, "medium")]
    [TestCase(ReasoningEffort.High, "high")]
    [TestCase(ReasoningEffort.ExtraHigh, "xhigh")]
    public async Task ChatClient_ReasoningEffort_IsSent(ReasoningEffort effort, string expected)
    {
        using FakeOpenAiServer server = new FakeOpenAiServer("application/json", ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions { Reasoning = new ReasoningOptions { Effort = effort } });

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["reasoning_effort"]?.Value<string>(), Is.EqualTo(expected));
    }

    [Test]
    public async Task ChatClient_Streaming_ReasoningEffort_IsSent()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer("text/event-stream", StreamSse);
        IChatClient client = CreateClient(server.Port);

        await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync(Prompt(), new ChatOptions { Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High } }))
        {
        }

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["reasoning_effort"]?.Value<string>(), Is.EqualTo("high"));
    }

    [Test]
    public async Task ChatClient_WithoutReasoning_SendsNoReasoningEffort()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer("application/json", ReplyJson);
        IChatClient client = CreateClient(server.Port);

        await client.GetResponseAsync(Prompt(), new ChatOptions());

        JObject body = JObject.Parse(server.RequestBodies.Single());
        Assert.That(body["reasoning_effort"], Is.Null);
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
        new global::Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "Hello.")
    ];

    private sealed class FakeOpenAiServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string contentType;
        private readonly string response;

        public int Port { get; }
        public List<string> RequestBodies { get; } = [];

        public FakeOpenAiServer(string contentType, string response)
        {
            this.contentType = contentType;
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
                context.Response.ContentType = contentType;
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
