using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Audio;
using LlmTornado.Audio.Google;
using LlmTornado.Audio.Models;
using LlmTornado.Audio.Vendors.Google;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Interactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class GeminiSpeechAndVoiceTests
{
    [TestCase("gemini-3.8-flash-tts")]
    [TestCase("gemini-3.8-flash-lite-tts")]
    public void TtsModels_AreRegisteredAsAudioModels(string name)
    {
        Assert.That(AudioModel.Google.Gemini.AllModels.Select(x => x.Name), Does.Contain(name));
        AudioModel model = name;
        Assert.That(model.Provider, Is.EqualTo(LLmProviders.Google));
    }

    [TestCase("mp3", "audio/mp3")]
    [TestCase("opus", "audio/ogg_opus")]
    [TestCase("wav", "audio/wav")]
    [TestCase("pcm", "audio/l16")]
    [TestCase("alaw", "audio/alaw")]
    [TestCase("mulaw", "audio/mulaw")]
    public void AudioFormats_AreNativeAndPreserveSampleRate(string format, string mimeType)
    {
        SpeechResponseFormat encoding = format switch
        {
            "mp3" => SpeechResponseFormat.Mp3, "opus" => SpeechResponseFormat.Opus,
            "wav" => SpeechResponseFormat.Wav, "pcm" => SpeechResponseFormat.Pcm,
            "alaw" => SpeechResponseFormat.Alaw, _ => SpeechResponseFormat.Mulaw
        };
        InteractionCreateRequest request = VendorGoogleSpeechHandler.CreateRequest(new SpeechRequest
        {
            Input = "Hello", Model = AudioModel.Google.Gemini.Gemini38FlashTts,
            VoiceId = "voice_saved", ResponseFormat = encoding, SampleRate = 8000,
            Google = new GoogleSpeechOptions { BitRate = 64000 }
        });
        JObject json = JObject.Parse(request.Serialize());
        Assert.That(json["response_format"]?["mime_type"]?.ToString(), Is.EqualTo(mimeType));
        Assert.That(json["response_format"]?["sample_rate"]?.Value<int>(), Is.EqualTo(8000));
        Assert.That(json["response_format"]?["bit_rate"]?.Value<int>(), Is.EqualTo(64000));
        Assert.That(json["generation_config"]?["speech_config"]?[0]?["voice"]?.ToString(), Is.EqualTo("voice_saved"));
        Assert.That(json["input"]?[0]?["type"]?.ToString(), Is.EqualTo("user_input"));
        Assert.That(json["store"]?.Value<bool>(), Is.False);
    }

    [Test]
    public void InteractionSpeech_RoundTripsAnnotationsFormatsAndImageMigration()
    {
        InteractionCreateRequest request = VendorGoogleSpeechHandler.CreateRequest(new SpeechRequest
        {
            Model = AudioModel.Google.Gemini.Gemini38FlashLiteTts, Instructions = "cheerful",
            Google = new GoogleSpeechOptions
            {
                Turns = [new() { Text = "[laughing] Hi!", Speaker = "Alice" }, new() { Text = "Hello!", Speaker = "Bob", Style = "softly" }],
                Speakers = [new() { Speaker = "Alice", Voice = "voice_designed", Language = "en" }, new() { Speaker = "Bob", Voice = "voicekey_replicated" }]
            }
        });
        request.GenerationConfig!.ImageConfig = new() { ImageSize = "1K" };
        JObject json = JObject.Parse(request.Serialize());
        Assert.That(json["input"]?[0]?["content"]?[0]?["text"]?.ToString(), Is.EqualTo("[laughing] Hi!"));
        Assert.That(json["input"]?[0]?["content"]?[0]?["annotations"]?[0]?["style"]?.ToString(), Is.EqualTo("cheerful"));
        Assert.That(json["input"]?[0]?["content"]?[1]?["annotations"]?[0]?["speaker"]?.ToString(), Is.EqualTo("Bob"));
        Assert.That(json["generation_config"]?["speech_config"]?[1]?["voice"]?.ToString(), Is.EqualTo("voicekey_replicated"));
        Assert.That(json["generation_config"]?["image_config"], Is.Null);
        var format = JsonConvert.DeserializeObject<InteractionResponseFormat>("""{"type":"audio","mime_type":"audio/mulaw","sample_rate":8000,"bit_rate":64000,"delivery":"inline"}""")!;
        Assert.That(format.MimeType, Is.EqualTo("audio/mulaw"));
        Assert.That(format.BitRate, Is.EqualTo(64000));
        Assert.That(format.Delivery, Is.EqualTo("inline"));
        var annotation = JsonConvert.DeserializeObject<InteractionAnnotation>("""{"type":"word_info","speaker":"spk_1","start_offset":"0.1s","end_offset":"0.2s"}""")!;
        Assert.That(JObject.Parse(JsonConvert.SerializeObject(annotation))["start_offset"]?.ToString(), Is.EqualTo("0.1s"));
    }

    [Test]
    public async Task CreateSpeech_UsesInteractionsAndDecodesAllAudioBlocks()
    {
        using LocalServer server = new();
        Task<SpeechTtsResult?> operation = server.Api.Audio.CreateSpeech(new SpeechRequest
        {
            Input = "Have a wonderful day!", Instructions = "cheerful and friendly",
            Model = AudioModel.Google.Gemini.Gemini38FlashTts,
            ResponseFormat = SpeechResponseFormat.Wav, Voice = SpeechVoice.Custom("Kore")
        });
        HttpListenerContext context = await server.Receive();
        JObject body = await Body(context);
        Assert.That(context.Request.Url!.AbsolutePath, Is.EqualTo("/v1beta/interactions"));
        Assert.That(context.Request.QueryString["key"], Is.EqualTo("test-key"));
        Assert.That(context.Request.Headers["Api-Revision"], Is.EqualTo("2026-05-20"));
        Assert.That(body["model"]?.ToString(), Is.EqualTo("gemini-3.8-flash-tts"));
        Assert.That(body["input"]?[0]?["content"]?[0]?["annotations"]?[0]?["type"]?.ToString(), Is.EqualTo("speech_metadata"));
        await Reply(context, """
            {"id":"int_speech","status":"completed","steps":[{"type":"model_output","content":[
            {"type":"audio","data":"AQID","mime_type":"audio/wav","sample_rate":24000,"channels":1},
            {"type":"audio","data":"BAU=","mime_type":"audio/wav","sample_rate":24000,"channels":1}]}],"usage":{"total_output_tokens":10}}
            """);
        SpeechTtsResult result = (await operation.WaitAsync(server.Timeout.Token))!;
        using (result.Response)
        using (result.AudioStream)
        using (MemoryStream bytes = new())
        {
            await result.AudioStream.CopyToAsync(bytes, server.Timeout.Token);
            Assert.That(bytes.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
            Assert.That(result.MimeType, Is.EqualTo("audio/wav"));
            Assert.That(result.SampleRate, Is.EqualTo(24000));
            Assert.That(result.GoogleInteraction!.Usage!.TotalOutputTokens, Is.EqualTo(10));
        }
    }

    [Test]
    public async Task StreamSpeech_DecodesChunksAndRetainsMetadataWithoutContainerHeaders()
    {
        using LocalServer server = new();
        List<SpeechTtsChunk> chunks = [];
        Task operation = Consume();
        HttpListenerContext context = await server.Receive();
        JObject body = await Body(context);
        Assert.That(body["stream"]?.Value<bool>(), Is.True);
        Assert.That(body["response_format"]?["mime_type"]?.ToString(), Is.EqualTo("audio/l16"));
        await Reply(context, """
            data: {"event_type":"step.delta","delta":{"type":"audio","data":"AQID","mime_type":"audio/l16","sample_rate":16000,"channels":1}}

            data: {"event_type":"step.delta","delta":{"type":"audio","data":"BAU="}}

            data: {"event_type":"interaction.completed","interaction":{"id":"int_stream","status":"completed"}}


            """, true);
        await operation.WaitAsync(server.Timeout.Token);
        Assert.That(chunks, Has.Count.EqualTo(2));
        Assert.That(chunks.SelectMany(x => x.Data), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
        Assert.That(chunks[1].MimeType, Is.EqualTo("audio/l16"));
        Assert.That(chunks[1].SampleRate, Is.EqualTo(16000));

        async Task Consume()
        {
            await foreach (SpeechTtsChunk chunk in server.Api.Audio.StreamSpeech(new SpeechRequest
            {
                Model = AudioModel.Google.Gemini.Gemini38FlashTts, Input = "Hello", ResponseFormat = SpeechResponseFormat.Pcm
            }, server.Timeout.Token)) chunks.Add(chunk);
        }
    }

    [Test]
    public async Task InteractionStream_DispatchesTypedAudioCallbacksAndLifecycleFromDataOnlySse()
    {
        using LocalServer server = new();
        List<InteractionContent> audio = [];
        Interaction? completed = null;
        InteractionStreamEventHandler handler = new()
        {
            OnAudioDelta = chunk => { audio.Add(chunk); return ValueTask.CompletedTask; },
            OnCompleted = result => { completed = result; return ValueTask.CompletedTask; }
        };
        Task operation = Consume();
        HttpListenerContext context = await server.Receive();
        await Reply(context, """
            data: {"event_type":"step.delta","delta":{"type":"audio","data":"AQID","mime_type":"audio/mulaw","sample_rate":8000,"channels":1}}

            data: {"event_type":"interaction.completed","interaction":{"id":"int_complete","status":"completed"}}


            """, true);
        await operation.WaitAsync(server.Timeout.Token);
        Assert.That(audio, Has.Count.EqualTo(1));
        Assert.That(audio[0].SampleRate, Is.EqualTo(8000));
        Assert.That(completed!.Id, Is.EqualTo("int_complete"));

        async Task Consume()
        {
            await foreach (var _ in server.Api.Interactions.CreateStream(new InteractionCreateRequest
            {
                Model = "gemini-3.8-flash-tts", Input = InteractionInput.FromText("Hello"), ResponseFormat = InteractionResponseFormat.Audio()
            }, handler, server.Timeout.Token)) { }
        }
    }

    [Test]
    public async Task StreamSpeech_CancellationInterruptsAWaitingAudioRead()
    {
        using LocalServer server = new();
        using CancellationTokenSource cancellation = new();
        await using IAsyncEnumerator<SpeechTtsChunk> stream = server.Api.Audio.StreamSpeech(new SpeechRequest
        {
            Model = AudioModel.Google.Gemini.Gemini38FlashTts, Input = "Hello", ResponseFormat = SpeechResponseFormat.Pcm
        }, cancellation.Token).GetAsyncEnumerator();
        Task<bool> first = stream.MoveNextAsync().AsTask();
        HttpListenerContext context = await server.Receive();
        context.Response.ContentType = "text/event-stream";
        context.Response.SendChunked = true;
        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("data: {\"event_type\":\"step.delta\",\"delta\":{\"type\":\"audio\",\"data\":\"AQID\"}}\n\n"), server.Timeout.Token);
        await context.Response.OutputStream.FlushAsync(server.Timeout.Token);
        Assert.That(await first.WaitAsync(server.Timeout.Token), Is.True);
        Task<bool> next = stream.MoveNextAsync().AsTask();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await next.WaitAsync(TimeSpan.FromSeconds(5)));
        context.Response.Close();
    }

    [Test]
    public async Task StreamSpeech_ThrowsOnProviderErrorEvent()
    {
        using LocalServer server = new();
        Task operation = Consume();
        HttpListenerContext context = await server.Receive();
        await Reply(context, "data: {\"event_type\":\"error\",\"error\":{\"message\":\"Speech failed\"}}\n\n", true);
        Assert.ThrowsAsync<InvalidDataException>(async () => await operation.WaitAsync(server.Timeout.Token));

        async Task Consume()
        {
            await foreach (var _ in server.Api.Audio.StreamSpeech(new SpeechRequest
            {
                Model = AudioModel.Google.Gemini.Gemini38FlashTts, Input = "Hello"
            }, server.Timeout.Token)) { }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Voices_CreateSupportsDesignAndStatelessReplication(bool replicated)
    {
        using LocalServer server = new();
        GoogleVoiceCreateRequest request = new()
        {
            Store = !replicated,
            Voice = new GoogleVoice
            {
                Model = "gemini-3.8-flash-tts", DisplayName = "Narrator", LanguageCode = "en-GB",
                Type = replicated ? GoogleVoiceType.Replicated : GoogleVoiceType.Prompted,
                Prompted = replicated ? null : new() { Input = "Warm adult voice", BaseVoice = "Puck" },
                Replicated = replicated ? new() { SourceAudio = new() { Data = "AQID" }, ConsentAudio = new() { Data = "BAUG" } } : null
            }
        };
        var operation = server.Api.Audio.GoogleVoices.CreateAsync(request, server.Timeout.Token);
        HttpListenerContext context = await server.Receive();
        JObject body = await Body(context);
        Assert.That(context.Request.Url!.AbsolutePath, Is.EqualTo("/v1beta/voices"));
        Assert.That(context.Request.QueryString["key"], Is.EqualTo("test-key"));
        Assert.That(body["store"]?.Value<bool>(), Is.EqualTo(!replicated));
        Assert.That(body["voice"]?["type"]?.ToString(), Is.EqualTo(replicated ? "replicated" : "prompted"));
        Assert.That(body["voice"]?["model"]?.ToString(), Is.EqualTo("gemini-3.8-flash-tts"));
        Assert.That(body["voice"]?[replicated ? "replicated" : "prompted"]?[replicated ? "consent_audio" : "base_voice"], Is.Not.Null);
        await Reply(context, replicated
            ? """{"type":"replicated","key":"voicekey_123","expire_time":"2026-10-28T00:00:00Z"}"""
            : """{"type":"prompted","id":"voice_123","sample_audio":{"mime_type":"audio/wav","data":"AQID"},"usage":{"total_output_tokens":25}}""");
        var result = await operation.WaitAsync(server.Timeout.Token);
        Assert.That(result.Ok, Is.True);
        if (replicated)
        {
            Assert.That(result.Data!.Key, Is.EqualTo("voicekey_123"));
            Assert.That(result.Data.ExpireTime, Is.Not.Null);
        }
        else
        {
            Assert.That(result.Data!.Id, Is.EqualTo("voice_123"));
            Assert.That(result.Data.SampleAudio!.Data, Is.EqualTo("AQID"));
            Assert.That(result.Data.Usage!.TotalOutputTokens, Is.EqualTo(25));
        }
    }

    [Test]
    public async Task Voices_ListPreservesRepeatedFiltersAndPagination_AndGetDeleteUseStoredIds()
    {
        using LocalServer server = new();
        var listTask = server.Api.Audio.GoogleVoices.ListAsync(new()
        {
            PageSize = 2, PageToken = "next+page/==", LanguageCode = ["en-US", "en-GB"],
            Type = ["prompted", "prebuilt"], Persona = ["Warm, Friendly"], Search = "British & calm"
        }, server.Timeout.Token);
        HttpListenerContext context = await server.Receive();
        Assert.That(context.Request.QueryString.GetValues("language_code"), Is.EqualTo(new[] { "en-US", "en-GB" }));
        Assert.That(context.Request.QueryString.GetValues("type"), Is.EqualTo(new[] { "prompted", "prebuilt" }));
        Assert.That(context.Request.QueryString["page_token"], Is.EqualTo("next+page/=="));
        Assert.That(context.Request.QueryString["search"], Is.EqualTo("British & calm"));
        await Reply(context, """{"voices":[{"id":"voice_custom","type":"prompted"},{"id":"Puck","type":"prebuilt"}],"next_page_token":"page2"}""");
        var list = await listTask.WaitAsync(server.Timeout.Token);
        Assert.That(list.Data!.NextPageToken, Is.EqualTo("page2"));
        Assert.That(list.Data.Voices[1].Type, Is.EqualTo(GoogleVoiceType.Prebuilt));

        var getTask = server.Api.Audio.GoogleVoices.GetAsync("voices/voice_custom", server.Timeout.Token);
        context = await server.Receive();
        Assert.That(context.Request.Url!.AbsolutePath, Is.EqualTo("/v1beta/voices/voice_custom"));
        await Reply(context, """{"id":"voice_custom","type":"prompted","display_name":"Narrator"}""");
        Assert.That((await getTask.WaitAsync(server.Timeout.Token)).Data!.DisplayName, Is.EqualTo("Narrator"));

        var deleteTask = server.Api.Audio.GoogleVoices.DeleteAsync("voice_custom", server.Timeout.Token);
        context = await server.Receive();
        Assert.That(context.Request.HttpMethod, Is.EqualTo("DELETE"));
        Assert.That(context.Request.Url!.AbsolutePath, Is.EqualTo("/v1beta/voices/voice_custom"));
        await Reply(context, "{}");
        Assert.That((await deleteTask.WaitAsync(server.Timeout.Token)).Ok, Is.True);
    }

    [Test]
    public async Task Voices_HttpErrorIsReturnedWithoutFabricatingVoiceData()
    {
        using LocalServer server = new();
        var operation = server.Api.Audio.GoogleVoices.GetAsync("voice_missing", server.Timeout.Token);
        HttpListenerContext context = await server.Receive();
        context.Response.StatusCode = 404;
        await Reply(context, """{"error":{"code":404,"message":"Voice not found","status":"NOT_FOUND"}}""");
        var result = await operation.WaitAsync(server.Timeout.Token);
        Assert.That(result.Code, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(result.Ok, Is.False);
        Assert.That(result.Data, Is.Null);
    }

    [Test]
    public void InvalidVoiceCreation_RequiresPromptStorageAndBothReplicationRecordings()
    {
        var voices = new TornadoApi(LLmProviders.Google, "test-key").Audio.GoogleVoices;
        Assert.Throws<ArgumentException>(() => voices.CreateAsync(new() { Store = false, Voice = new() { Prompted = new() { Input = "Warm" } } }));
        Assert.Throws<ArgumentException>(() => voices.CreateAsync(new() { Voice = new() { Type = GoogleVoiceType.Replicated, Replicated = new() { SourceAudio = new() { Data = "AQID" } } } }));
        Assert.Throws<ArgumentException>(() => voices.CreateAsync(new() { Voice = new() { Type = GoogleVoiceType.Prebuilt } }));
    }

    [TestCase("get", "GET")]
    [TestCase("cancel", "POST")]
    [TestCase("delete", "DELETE")]
    public async Task InteractionActions_ResolveAbsoluteProviderUrlsAndResourceNames(string action, string method)
    {
        using LocalServer server = new();
        Task operation = action switch
        {
            "get" => server.Api.Interactions.Get("interactions/int_speech", server.Timeout.Token),
            "cancel" => server.Api.Interactions.Cancel("int_speech", server.Timeout.Token),
            _ => server.Api.Interactions.Delete("int_speech", server.Timeout.Token)
        };
        HttpListenerContext context = await server.Receive();
        Assert.That(context.Request.HttpMethod, Is.EqualTo(method));
        Assert.That(context.Request.Url!.AbsolutePath, Is.EqualTo("/v1beta/interactions/int_speech" + (action == "cancel" ? ":cancel" : "")));
        await Reply(context, action == "delete" ? "{}" : """{"id":"int_speech","status":"completed"}""");
        await operation.WaitAsync(server.Timeout.Token);
    }

    private static async Task<JObject> Body(HttpListenerContext context)
    {
        using StreamReader reader = new(context.Request.InputStream);
        return JObject.Parse(await reader.ReadToEndAsync());
    }

    private static async Task Reply(HttpListenerContext context, string text, bool stream = false)
    {
        context.Response.ContentType = stream ? "text/event-stream" : "application/json";
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private sealed class LocalServer : IDisposable
    {
        private readonly HttpListener listener = new();
        public CancellationTokenSource Timeout { get; } = new(TimeSpan.FromSeconds(20));
        public TornadoApi Api { get; }

        public LocalServer()
        {
            using TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            string prefix = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Add(prefix);
            listener.Start();
            Api = new TornadoApi(LLmProviders.Google, "test-key");
            ((GoogleEndpointProvider)Api.GetProvider(LLmProviders.Google)).UrlResolver =
                (endpoint, path, _) => prefix + "v1beta/" + GoogleEndpointProvider.GetEndpointUrlFragment(endpoint) + path;
        }

        public Task<HttpListenerContext> Receive() => listener.GetContextAsync().WaitAsync(Timeout.Token);
        public void Dispose() { listener.Close(); Timeout.Dispose(); }
    }
}
