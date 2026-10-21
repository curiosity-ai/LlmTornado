using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;

namespace LlmTornado.Tests;

[TestFixture]
public class MeaiFinishReasonTests
{
    [TestCase("refusal", "content_filter")]
    [TestCase("recitation", "content_filter")]
    [TestCase("prohibited_content", "content_filter")]
    [TestCase("image_safety", "content_filter")]
    [TestCase("model_context_window_exceeded", "length")]
    public async Task ChatClient_MapsProviderStopReasonsToTheClosestFinishReason(string reason, string expected)
    {
        string json = $$$"""
            {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"gpt-4o",
             "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"{{{reason}}}"}],
             "usage":{"prompt_tokens":5,"completion_tokens":1,"total_tokens":6}}
            """;

        using FakeOpenAiServer server = new FakeOpenAiServer("application/json", json);
        IChatClient client = CreateClient(server.Port);

        ChatResponse response = await client.GetResponseAsync(Prompt());

        Assert.That(response.FinishReason?.Value, Is.EqualTo(expected));
    }

    [TestCase("refusal", "content_filter")]
    [TestCase("model_context_window_exceeded", "length")]
    public async Task ChatClient_Streaming_MapsProviderStopReasonsToTheClosestFinishReason(string reason, string expected)
    {
        string sse = $$$"""
            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"ok"},"finish_reason":null}]}

            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"{{{reason}}}"}]}

            data: [DONE]


            """;

        using FakeOpenAiServer server = new FakeOpenAiServer("text/event-stream", sse);
        IChatClient client = CreateClient(server.Port);

        ChatFinishReason? finishReason = null;
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(Prompt()))
        {
            finishReason = update.FinishReason ?? finishReason;
        }

        Assert.That(finishReason?.Value, Is.EqualTo(expected));
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
                    await reader.ReadToEndAsync().ConfigureAwait(false);
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
