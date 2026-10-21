using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Audio;
using LlmTornado.Audio.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class SpeechStreamTests
{
    [Test]
    public void SpeechRequest_SendsNoStreamFormatByDefault()
    {
        string json = JsonConvert.SerializeObject(new SpeechRequest { Input = "hi" });

        Assert.That(json, Does.Not.Contain("stream_format"));
    }

    [Test]
    public void SpeechRequest_SerializesTheSseStreamFormat()
    {
        string json = JsonConvert.SerializeObject(new SpeechRequest { Input = "hi", StreamFormat = SpeechStreamFormats.Sse });

        Assert.That(json, Does.Contain("\"stream_format\":\"sse\""));
    }

    [Test]
    public async Task InboundStream_ReadsSpeechAudioDeltasAndUsage()
    {
        const string sse = """
            data: {"type":"speech.audio.delta","audio":"AQID"}

            data: {"type":"speech.audio.delta","audio":"BAU="}

            data: {"type":"speech.audio.done","usage":{"input_tokens":14,"output_tokens":101,"total_tokens":115}}

            """ + "\n";

        OpenAiEndpointProvider provider = new OpenAiEndpointProvider(LLmProviders.OpenAi);
        StreamReader reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sse)));
        List<SpeechStreamEvent> events = [];

        await foreach (SpeechStreamEvent? x in provider.InboundStream<SpeechStreamEvent>(reader))
        {
            if (x is not null)
            {
                events.Add(x);
            }
        }

        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0].AudioBytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(events[1].AudioBytes, Is.EqualTo(new byte[] { 4, 5 }));
        Assert.That(events[2].Type, Is.EqualTo("speech.audio.done"));
        Assert.That(events[2].Usage?.TotalTokens, Is.EqualTo(115));
        Assert.That(events[2].AudioBytes, Is.Empty);
    }

    [Test]
    public async Task StreamSpeechEvents_PostsTheSseStreamFormatAndReturnsEveryEvent()
    {
        const string sse = """
            data: {"type":"speech.audio.delta","audio":"AQID"}

            data: {"type":"speech.audio.delta","audio":"BAU="}

            data: {"type":"speech.audio.done","usage":{"input_tokens":14,"output_tokens":101,"total_tokens":115}}


            """;

        using FakeSpeechServer server = new FakeSpeechServer(sse);
        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });

        List<SpeechStreamEvent> events = [];

        // the caller does not set StreamFormat, StreamSpeechEvents has to
        await foreach (SpeechStreamEvent x in api.Audio.StreamSpeechEvents(new SpeechRequest { Input = "hi", Model = AudioModel.OpenAi.Tts.Gpt4OMiniTts20251215 }))
        {
            events.Add(x);
        }

        Assert.That(server.Path, Is.EqualTo("/v1/audio/speech"));
        Assert.That(server.RequestBody, Does.Contain("\"stream_format\":\"sse\""));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0].AudioBytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(events[1].AudioBytes, Is.EqualTo(new byte[] { 4, 5 }));
        Assert.That(events[2].Usage?.InputTokens, Is.EqualTo(14));
        Assert.That(events[2].Usage?.OutputTokens, Is.EqualTo(101));
        Assert.That(events[2].Usage?.TotalTokens, Is.EqualTo(115));
    }

    [Test]
    public async Task StreamSpeech_ReturnsTheDecodedAudioChunksOfAnOpenAiStream()
    {
        const string sse = """
            data: {"type":"speech.audio.delta","audio":"AQID"}

            data: {"type":"speech.audio.delta","audio":"BAU="}

            data: {"type":"speech.audio.done","usage":{"input_tokens":14,"output_tokens":101,"total_tokens":115}}


            """;

        using FakeSpeechServer server = new FakeSpeechServer(sse);
        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });

        List<byte[]> chunks = [];

        await foreach (SpeechTtsChunk chunk in api.Audio.StreamSpeech(new SpeechRequest { Input = "hi", Model = AudioModel.OpenAi.Tts.Gpt4OMiniTts20251215 }))
        {
            chunks.Add(chunk.Data);
        }

        Assert.That(server.RequestBody, Does.Contain("\"stream_format\":\"sse\""));
        Assert.That(chunks, Has.Count.EqualTo(2));
        Assert.That(chunks[0], Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(chunks[1], Is.EqualTo(new byte[] { 4, 5 }));
    }

    private sealed class FakeSpeechServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string response;

        public int Port { get; }
        public string RequestBody { get; private set; } = string.Empty;
        public string Path { get; private set; } = string.Empty;

        public FakeSpeechServer(string response)
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
                RequestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            Path = context.Request.Url?.AbsolutePath ?? string.Empty;

            byte[] bytes = Encoding.UTF8.GetBytes(response);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/event-stream";
            await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            context.Response.Close();
        }

        public void Dispose()
        {
            listener.Close();
        }
    }
}
