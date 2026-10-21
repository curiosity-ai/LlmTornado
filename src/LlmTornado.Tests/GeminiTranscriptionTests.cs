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
public class GeminiTranscriptionTests
{
    private static TranscriptionRequest Request() => new()
    {
        Model = AudioModel.Google.Gemini.Gemini35Transcribe,
        File = new AudioFile([1, 2, 3], AudioFileTypes.Wav)
    };

    [TestCase("bytes")]
    [TestCase("stream")]
    [TestCase("data_url")]
    [TestCase("file_uri")]
    public async Task PublicSerialization_UsesNativeAudioContentAndDoesNotDisposeCallerStream(string input)
    {
        using MemoryStream stream = new([1, 2, 3]);
        TranscriptionRequest request = Request();
        if (input == "stream") request.File = new AudioFile(stream, AudioFileTypes.Wav);
        if (input is "data_url" or "file_uri")
        {
            request.File = null!;
            request.Url = input == "data_url" ? "data:audio/wav;base64,AQID" : "https://generativelanguage.googleapis.com/v1beta/files/audio123";
            if (input == "file_uri") request.Google = new GoogleTranscriptionOptions { MimeType = "audio/wav" };
        }
        var provider = new TornadoApi(LLmProviders.Google, "test-key").GetProvider(LLmProviders.Google);
        var serialized = await request.SerializeAsync(provider);
        JObject body = JObject.Parse(serialized.Body.ToString()!);
        Assert.That(serialized.Url, Does.EndWith("/v1beta/interactions"));
        Assert.That(body["model"]?.ToString(), Is.EqualTo("gemini-3.5-transcribe"));
        Assert.That(body["input"]?[0]?["type"]?.ToString(), Is.EqualTo("audio"));
        Assert.That(body["input"]?[0]?["mime_type"]?.ToString(), Is.EqualTo("audio/wav"));
        Assert.That(body["input"]?[0]?[input == "file_uri" ? "uri" : "data"]?.ToString(), Is.EqualTo(input == "file_uri" ? request.Url : "AQID"));
        Assert.That(body["store"]?.Value<bool>(), Is.False);
        Assert.That(body["file"], Is.Null);
        Assert.That(stream.CanRead, Is.True);
    }

    [Test]
    public async Task CommonRecognitionControls_MergeWithoutMutatingNativeOptions()
    {
        TranscriptionRequest request = Request();
        request.Language = "en-GB";
        request.Diarize = true;
        request.TimestampGranularities = [TimestampGranularities.Word];
        request.Google = new GoogleTranscriptionOptions
        {
            Configuration = new InteractionTranscriptionConfig { Mode = InteractionTranscriptionMode.Verbatim() }
        };
        InteractionCreateRequest native = await VendorGoogleTranscriptionHandler.CreateRequest(request);
        JObject body = JObject.Parse(native.Serialize());
        JToken config = body["generation_config"]!["transcription_config"]!;
        Assert.That(config["language_codes"]?.Values<string>(), Is.EqualTo(new[] { "en-GB" }));
        Assert.That(config["mode"]?["type"]?.ToString(), Is.EqualTo("verbatim"));
        Assert.That(config["mode"]?["diarization_mode"]?.ToString(), Is.EqualTo("speaker"));
        Assert.That(config["mode"]?["timestamp_granularities"]?.Values<string>(), Is.EqualTo(new[] { "word" }));
        Assert.That(config["diarization_mode"], Is.Null);
        Assert.That(request.Google.Configuration!.Mode!.DiarizationMode, Is.Null);
        Assert.That(request.Google.Configuration.Mode.TimestampGranularities, Is.Null);
    }

    [Test]
    public async Task SmartRecognition_PreservesNativeLanguageHintsAndVocabulary()
    {
        TranscriptionRequest request = Request();
        request.Language = "en";
        request.ContextBias = ["legacy hint"];
        request.Google = new()
        {
            Configuration = new()
            {
                LanguageCodes = ["cs-CZ", "en-US"], CustomVocabulary = ["Kubernetes"], Mode = InteractionTranscriptionMode.Smart()
            }
        };
        var native = await VendorGoogleTranscriptionHandler.CreateRequest(request);
        JObject body = JObject.Parse(native.Serialize());
        var config = body["generation_config"]!["transcription_config"]!;
        Assert.That(config["mode"]?["type"]?.ToString(), Is.EqualTo("smart"));
        Assert.That(config["language_codes"]?.Values<string>(), Is.EqualTo(new[] { "cs-CZ", "en-US" }));
        Assert.That(config["custom_vocabulary"]?.Values<string>(), Is.EqualTo(new[] { "Kubernetes" }));
    }

    [TestCase("smart_diarization")]
    [TestCase("smart_timestamps")]
    [TestCase("vocabulary_diarization")]
    [TestCase("vocabulary_timestamps")]
    [TestCase("too_many_terms")]
    [TestCase("segment_timestamps")]
    public void InvalidRecognitionCombinations_AreRejectedBeforeReadingAudio(string conflict)
    {
        TranscriptionRequest request = Request();
        request.Google = new() { Configuration = new() { Mode = conflict.StartsWith("smart") ? InteractionTranscriptionMode.Smart() : InteractionTranscriptionMode.Verbatim() } };
        if (conflict.EndsWith("diarization")) request.Diarize = true;
        if (conflict.EndsWith("timestamps")) request.TimestampGranularities = [conflict == "segment_timestamps" ? TimestampGranularities.Segment : TimestampGranularities.Word];
        if (conflict.StartsWith("vocabulary")) request.Google.Configuration.CustomVocabulary = ["Kubernetes"];
        if (conflict == "too_many_terms") request.ContextBias = Enumerable.Range(0, 1001).Select(i => "term" + i).ToList();
        Assert.ThrowsAsync<ArgumentException>(() => VendorGoogleTranscriptionHandler.CreateRequest(request));
    }

    [TestCase("\"smart\"", "smart")]
    [TestCase("\"verbatim\"", "verbatim")]
    [TestCase("{\"type\":\"verbatim\",\"diarization_mode\":\"speaker\",\"timestamp_granularities\":[\"word\"]}", "verbatim")]
    public void InteractionConfiguration_ReadsBothModeFormsAndSurvivesSchemaMigration(string json, string type)
    {
        var mode = JsonConvert.DeserializeObject<InteractionTranscriptionMode>(json)!;
        Assert.That(mode.Type, Is.EqualTo(type));
        InteractionCreateRequest request = new()
        {
            Model = "gemini-3.5-transcribe", Input = InteractionInput.FromText("test"),
            GenerationConfig = new()
            {
                ImageConfig = new() { ImageSize = "1K" }, TranscriptionConfig = new() { Mode = mode }
            }
        };
        JObject serialized = JObject.Parse(request.Serialize());
        Assert.That(serialized["generation_config"]?["transcription_config"]?["mode"]?["type"]?.ToString(), Is.EqualTo(type));
        Assert.That(serialized["generation_config"]?["image_config"], Is.Null);
    }

    [Test]
    public void RecognitionResults_PreserveTextSpeakerAndNanosecondOffsetsAcrossOutputBlocks()
    {
        var interaction = JsonConvert.DeserializeObject<Interaction>(Transcript)!;
        TranscriptionResult result = VendorGoogleTranscriptionHandler.ToResult(interaction);
        Assert.That(result.Text, Is.EqualTo("Hello world. Again."));
        Assert.That(result.Words.Select(x => x.Word), Is.EqualTo(new[] { "Hello", "world", "Again" }));
        Assert.That(result.Words[0].Speaker, Is.EqualTo("spk_1"));
        Assert.That(result.Words[1].Speaker, Is.EqualTo("spk_2"));
        Assert.That(result.Words[0].Start, Is.EqualTo(0.1f).Within(0.0001));
        Assert.That(result.Words[0].End, Is.EqualTo(0.450123457f).Within(0.0001));
        Assert.That(result.Words[0].EndOffset, Is.EqualTo("0.450123456s"));
        Assert.That(result.Words[2].StartOffset, Is.Null);
        Assert.That(result.GoogleInteraction, Is.SameAs(interaction));
        Assert.That(result.Usage!.InputTokens, Is.EqualTo(100));
        Assert.That(result.Usage.TotalTokens, Is.EqualTo(110));
        Assert.That(result.Language, Is.Null, "language hints must not be presented as detected language");
    }

    [TestCase(200)]
    [TestCase(400)]
    public async Task PublicTranscription_UsesNativeJsonPathAndResponseHandling(int status)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        string prefix = $"http://127.0.0.1:{port}/";
        using HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();
        TornadoApi api = new(LLmProviders.Google, "test-key");
        ((GoogleEndpointProvider)api.GetProvider(LLmProviders.Google)).UrlResolver =
            (endpoint, path, _) => prefix + "v1beta/" + GoogleEndpointProvider.GetEndpointUrlFragment(endpoint) + path;
        TranscriptionRequest request = Request();
        request.Diarize = true;
        request.TimestampGranularities = [TimestampGranularities.Word];
        Task<TranscriptionResult?> operation = api.Audio.CreateTranscription(request);
        HttpListenerContext context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using StreamReader reader = new(context.Request.InputStream);
        JObject body = JObject.Parse(await reader.ReadToEndAsync(timeout.Token));
        Assert.That(context.Request.Url!.AbsolutePath, Is.EqualTo("/v1beta/interactions"));
        Assert.That(context.Request.QueryString["key"], Is.EqualTo("test-key"));
        Assert.That(context.Request.Headers["Api-Revision"], Is.EqualTo("2026-05-20"));
        Assert.That(context.Request.ContentType, Does.StartWith("application/json"));
        Assert.That(body["input"]?[0]?["data"]?.ToString(), Is.EqualTo("AQID"));
        context.Response.StatusCode = status;
        byte[] bytes = Encoding.UTF8.GetBytes(status == 200 ? Transcript : """{"error":{"code":400,"message":"Invalid audio input","status":"INVALID_ARGUMENT"}}""");
        await context.Response.OutputStream.WriteAsync(bytes, timeout.Token);
        context.Response.Close();
        if (status == 200)
        {
            var result = (await operation.WaitAsync(timeout.Token))!;
            Assert.That(result.Text, Is.EqualTo("Hello world. Again."));
            Assert.That(result.Words[1].Speaker, Is.EqualTo("spk_2"));
        }
        else Assert.CatchAsync<HttpRequestException>(async () => await operation.WaitAsync(timeout.Token));
    }

    [Test]
    public void CanceledSerialization_DoesNotReadCallerAudio()
    {
        TranscriptionRequest request = Request();
        request.CancellationToken = new CancellationToken(true);
        Assert.CatchAsync<OperationCanceledException>(() => VendorGoogleTranscriptionHandler.CreateRequest(request));
    }

    private const string Transcript = """
        {"id":"int_transcript","model":"gemini-3.5-transcribe","status":"completed","steps":[
          {"type":"user_input","content":[{"type":"text","text":"must not enter transcript"}]},
          {"type":"model_output","content":[{"type":"text","text":"Hello world.","annotations":[
            {"type":"word_info","text":"Hello","speaker":"spk_1","start_offset":"0.100s","end_offset":"0.450123456s"},
            {"type":"word_info","text":"world","speaker":"spk_2","start_offset":"0.500s","end_offset":"0.850s"}]}]},
          {"type":"model_output","content":[{"type":"text","text":" Again.","annotations":[{"type":"word_info","text":"Again","speaker":"spk_2"}]}]}],
          "usage":{"total_input_tokens":100,"total_output_tokens":10,"total_tokens":110}}
        """;
}
