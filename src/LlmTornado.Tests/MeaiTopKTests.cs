using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class MeaiTopKTests
{
    private static JObject Serialize(ChatRequest request, LLmProviders provider)
    {
        IEndpointProvider endpoint = new TornadoApi(provider, "test-key").GetProvider(provider);
        return JObject.Parse(request.Serialize(endpoint).Body.ToString()!);
    }

    private static ChatRequest Request(ChatModel model, int? topK)
    {
        return new ChatRequest
        {
            Model = model,
            TopK = topK,
            Messages = [new LlmTornado.Chat.ChatMessage(ChatMessageRoles.User, "Hello")]
        };
    }

    private const string AnthropicReply = """
        {"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-5-20250929",
         "content":[{"type":"text","text":"hi"}],"stop_reason":"end_turn","usage":{"input_tokens":1,"output_tokens":1}}
        """;

    [Test]
    public async Task ChatClient_TopK_IsSent()
    {
        using FakeServer server = new FakeServer(AnthropicReply);
        TornadoApi api = new TornadoApi(new AnthropicEndpointProvider
        {
            Auth = new ProviderAuthentication(LLmProviders.Anthropic, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });
        IChatClient client = api.AsChatClient(ChatModel.Anthropic.Claude45.Sonnet250929);

        await client.GetResponseAsync(
            [new global::Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "Hello")],
            new ChatOptions { TopK = 40 });

        Assert.That(JObject.Parse(server.RequestBodies.Single())["top_k"]?.Value<int>(), Is.EqualTo(40));
    }

    [Test]
    public void Anthropic_TopK_IsSent()
    {
        JObject body = Serialize(Request(ChatModel.Anthropic.Claude45.Sonnet250929, 40), LLmProviders.Anthropic);

        Assert.That(body["top_k"]?.Value<int>(), Is.EqualTo(40));
    }

    [Test]
    public void Anthropic_TopKUnset_IsOmitted()
    {
        JObject body = Serialize(Request(ChatModel.Anthropic.Claude45.Sonnet250929, null), LLmProviders.Anthropic);

        Assert.That(body.ContainsKey("top_k"), Is.False);
    }

    [Test]
    public void Anthropic_ModelThatRejectsSamplingParams_OmitsTopK()
    {
        JObject body = Serialize(Request(ChatModel.Anthropic.Claude5.Opus, 40), LLmProviders.Anthropic);

        Assert.That(body.ContainsKey("top_k"), Is.False);
    }

    [Test]
    public void Google_TopK_IsSentInGenerationConfig()
    {
        JObject body = Serialize(Request(ChatModel.Google.Gemini.Gemini37Flash, 25), LLmProviders.Google);

        Assert.That(body["generationConfig"]?["topK"]?.Value<int>(), Is.EqualTo(25));
    }

    [Test]
    public void OpenAi_TopK_IsNotSent()
    {
        JObject body = Serialize(Request(ChatModel.OpenAi.Gpt4.O, 40), LLmProviders.OpenAi);

        Assert.That(body.ContainsKey("top_k"), Is.False);
    }

    private sealed class FakeServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string response;

        public int Port { get; }
        public List<string> RequestBodies { get; } = [];

        public FakeServer(string response)
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
