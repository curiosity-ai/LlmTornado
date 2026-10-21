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
public class MeaiUsageDetailsTests
{
    private const string ReplyJson = """
        {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"gpt-4o",
         "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":50,"completion_tokens":30,"total_tokens":80,"prompt_tokens_details":{"cached_tokens":40},"completion_tokens_details":{"reasoning_tokens":12}}}
        """;

    private const string PlainReplyJson = """
        {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"gpt-4o",
         "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":5,"completion_tokens":1,"total_tokens":6}}
        """;

    private const string StreamSse = """
        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"ok"},"finish_reason":null}]}

        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[],"usage":{"prompt_tokens":50,"completion_tokens":30,"total_tokens":80,"prompt_tokens_details":{"cached_tokens":40},"completion_tokens_details":{"reasoning_tokens":12}}}

        data: [DONE]


        """;

    [Test]
    public async Task ChatClient_Usage_CarriesCachedAndReasoningTokens()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer("application/json", ReplyJson);
        IChatClient client = CreateClient(server.Port);

        ChatResponse response = await client.GetResponseAsync(Prompt());

        Assert.That(response.Usage?.InputTokenCount, Is.EqualTo(50));
        Assert.That(response.Usage?.OutputTokenCount, Is.EqualTo(30));
        Assert.That(response.Usage?.TotalTokenCount, Is.EqualTo(80));
        Assert.That(response.Usage?.CachedInputTokenCount, Is.EqualTo(40));
        Assert.That(response.Usage?.ReasoningTokenCount, Is.EqualTo(12));
    }

    [Test]
    public async Task ChatClient_Streaming_Usage_CarriesCachedAndReasoningTokens()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer("text/event-stream", StreamSse);
        IChatClient client = CreateClient(server.Port);

        List<UsageDetails> usages = [];
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(Prompt()))
        {
            usages.AddRange(update.Contents.OfType<UsageContent>().Select(x => x.Details));
        }

        UsageDetails usage = usages.Single();
        Assert.That(usage.InputTokenCount, Is.EqualTo(50));
        Assert.That(usage.CachedInputTokenCount, Is.EqualTo(40));
        Assert.That(usage.ReasoningTokenCount, Is.EqualTo(12));
    }

    [Test]
    public async Task ChatClient_Usage_WithoutDetails_LeavesThemUnset()
    {
        using FakeOpenAiServer server = new FakeOpenAiServer("application/json", PlainReplyJson);
        IChatClient client = CreateClient(server.Port);

        ChatResponse response = await client.GetResponseAsync(Prompt());

        Assert.That(response.Usage?.InputTokenCount, Is.EqualTo(5));
        Assert.That(response.Usage?.CachedInputTokenCount, Is.Null);
        Assert.That(response.Usage?.ReasoningTokenCount, Is.Null);
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
