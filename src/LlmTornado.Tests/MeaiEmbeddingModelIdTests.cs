using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class MeaiEmbeddingModelIdTests
{
    private const string EmbeddingJson = """
        {"object":"list","data":[{"object":"embedding","index":0,"embedding":[0.1,0.2]}],"model":"echo","usage":{"prompt_tokens":1,"total_tokens":1}}
        """;

    [Test]
    public async Task GenerateAsync_UsesTheModelIdOfTheOptions()
    {
        using CapturingServer server = new CapturingServer(EmbeddingJson);
        IEmbeddingGenerator<string, Embedding<float>> generator = CreateGenerator(server.Port, "text-embedding-3-small");

        await generator.GenerateAsync(["hello"], new EmbeddingGenerationOptions { ModelId = "text-embedding-3-large" });

        Assert.That(JObject.Parse(server.RequestBodies.Single())["model"]?.ToString(), Is.EqualTo("text-embedding-3-large"));
    }

    [Test]
    public async Task GenerateAsync_WithoutModelIdUsesTheDefaultModel()
    {
        using CapturingServer server = new CapturingServer(EmbeddingJson);
        IEmbeddingGenerator<string, Embedding<float>> generator = CreateGenerator(server.Port, "text-embedding-3-small");

        await generator.GenerateAsync(["hello"]);

        Assert.That(JObject.Parse(server.RequestBodies.Single())["model"]?.ToString(), Is.EqualTo("text-embedding-3-small"));
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(int port, string defaultModel)
    {
        TornadoApi api = new TornadoApi(new Uri($"http://127.0.0.1:{port}/v1"), "test-key", LLmProviders.OpenAi);
        return api.AsEmbeddingGenerator(defaultModel);
    }

    private sealed class CapturingServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string response;

        public int Port { get; }
        public List<string> RequestBodies { get; } = [];

        public CapturingServer(string response)
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

        public void Dispose() => listener.Close();
    }
}
