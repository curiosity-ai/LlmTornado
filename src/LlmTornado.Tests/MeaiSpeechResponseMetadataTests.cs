using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;

namespace LlmTornado.Tests;

[TestFixture]
public class MeaiSpeechResponseMetadataTests
{
    private const string Reply = """{"task":"transcribe","language":"english","duration":2.5,"text":"Hello world","segments":[]}""";

    private static ISpeechToTextClient CreateClient(FakeServer server)
    {
        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });

        return new TornadoSpeechToTextClient(api, "whisper-1");
    }

    [Test]
    public async Task GetText_ReportsModelRawResultAndDuration()
    {
        using FakeServer server = new FakeServer(Reply);
        ISpeechToTextClient client = CreateClient(server);

        SpeechToTextResponse response = await client.GetTextAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        Assert.That(response.Text, Is.EqualTo("Hello world"));
        Assert.That(response.ModelId, Is.EqualTo("whisper-1"));
        Assert.That(response.RawRepresentation, Is.InstanceOf<LlmTornado.Audio.TranscriptionResult>());
        Assert.That(response.StartTime, Is.EqualTo(TimeSpan.Zero));
        Assert.That(response.EndTime, Is.EqualTo(TimeSpan.FromSeconds(2.5)));
    }

    [Test]
    public async Task GetText_OptionsModelId_IsReported()
    {
        using FakeServer server = new FakeServer(Reply);
        ISpeechToTextClient client = CreateClient(server);

        SpeechToTextResponse response = await client.GetTextAsync(new MemoryStream(new byte[] { 1, 2, 3 }), new SpeechToTextOptions { ModelId = "gpt-4o-transcribe" });

        Assert.That(response.ModelId, Is.EqualTo("gpt-4o-transcribe"));
    }

    [Test]
    public async Task GetStreamingText_UpdateCarriesModelAndRawResult()
    {
        using FakeServer server = new FakeServer(Reply);
        ISpeechToTextClient client = CreateClient(server);

        List<SpeechToTextResponseUpdate> updates = [];
        await foreach (SpeechToTextResponseUpdate update in client.GetStreamingTextAsync(new MemoryStream(new byte[] { 1, 2, 3 })))
        {
            updates.Add(update);
        }

        Assert.That(updates, Has.Count.EqualTo(1));
        Assert.That(updates[0].Text, Is.EqualTo("Hello world"));
        Assert.That(updates[0].ModelId, Is.EqualTo("whisper-1"));
        Assert.That(updates[0].RawRepresentation, Is.InstanceOf<LlmTornado.Audio.TranscriptionResult>());
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
