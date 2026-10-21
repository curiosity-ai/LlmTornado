using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Audio.OpenAi;
using LlmTornado.Code;
using LlmTornado.Decision.OpenAi;
using LlmTornado.Live.OpenAi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class OpenAiNativeDecisionAndVoiceTests
{
    private static readonly JsonSerializerSettings Settings = new() { NullValueHandling = NullValueHandling.Ignore };

    private static List<OpenAiDecisionQuestion> Questions() =>
    [
        new OpenAiDecisionPredicate { Name = "damaged", Instructions = "Is the item damaged?" },
        new OpenAiDecisionChoice
        {
            Name = "route", Instructions = "Which route?",
            Choices = [new() { Value = true }, new() { Value = "true" }, new() { Value = "other" }]
        },
        new OpenAiDecisionScore
        {
            Name = "severity", Instructions = "Rate severity",
            Levels = [new() { Label = "minor" }, new() { Label = "major", Description = "No workaround" }]
        }
    ];

    [Test]
    public void NativeContract_UsesOrderedQuestionsAndPreservesBooleanChoices()
    {
        OpenAiDecisionRequest request = new() { Input = "Broken screen", Questions = Questions() };
        JObject body = JObject.Parse(request.Serialize());
        Assert.That(body["model"]!.ToString(), Is.EqualTo("gpt-6-luna"));
        Assert.That(body["state"], Is.Null);
        Assert.That(body["questions"]!.Type, Is.EqualTo(JTokenType.Array));
        Assert.That(body["questions"]![0]!["type"]!.ToString(), Is.EqualTo("predicate"));
        Assert.That(body["questions"]![1]!["choices"]![0]!["value"]!.Type, Is.EqualTo(JTokenType.Boolean));
        Assert.That(body["questions"]![1]!["choices"]![1]!["value"]!.Type, Is.EqualTo(JTokenType.String));
        Assert.That(body["questions"]![2]!["levels"]![1]!["label"]!.ToString(), Is.EqualTo("major"));
    }

    [Test]
    public void NativeMultimodalInput_PreservesImagesCachePartitionAndSafetyIdentifier()
    {
        JArray input = new(new JObject
        {
            ["role"] = "user",
            ["content"] = new JArray(new JObject { ["type"] = "input_text", ["text"] = "Inspect this" },
                new JObject { ["type"] = "input_image", ["image_url"] = "data:image/png;base64,AQID", ["detail"] = "original" })
        });
        OpenAiDecisionRequest request = new()
        {
            Input = input, Questions = Questions(), PromptCacheKey = "tenant-1", SafetyIdentifier = "user-1",
            PromptCacheOptions = new() { Mode = LlmTornado.Responses.ResponsePromptCacheMode.Explicit, Ttl = OpenAiDecisionCacheTtl.ThirtyMinutes }
        };
        JObject body = JObject.Parse(request.Serialize());
        Assert.That(JToken.DeepEquals(body["input"], input), Is.True);
        Assert.That(body["prompt_cache_key"]!.ToString(), Is.EqualTo("tenant-1"));
        Assert.That(body["safety_identifier"]!.ToString(), Is.EqualTo("user-1"));
        Assert.That(body["prompt_cache_options"]!["ttl"]!.ToString(), Is.EqualTo("30m"));
    }

    [Test]
    public void LiveConfig_DelegatesAndTriggersNativeDecisions()
    {
        OpenAiLiveSessionConfig config = OpenAiLiveSessionConfig.ForWebRtc();
        config.Delegation = OpenAiLiveDelegationConfig.DecisionsBackend(new() { Questions = Questions() });
        config.Triggers = [new() { Decisions = new() { Questions = Questions() } }];
        config.Client = new() { DataChannel = new()
        {
            AllowedClientEvents = new[] { "decision.item.create" },
            AllowedServerEvents = new List<OpenAiLiveServerEventSelector>
            {
                new() { Type = "decision.event" }, new() { Type = "session.input_transcript.delta" }
            }
        }};
        JObject body = JObject.Parse(JsonConvert.SerializeObject(config, Settings));
        Assert.That(body["delegation"]!["type"]!.ToString(), Is.EqualTo("decisions"));
        Assert.That(body["delegation"]!["decisions"]!["model"]!.ToString(), Is.EqualTo("gpt-6-luna"));
        Assert.That(body["triggers"]![0]!["event_type"]!.ToString(), Is.EqualTo("session.input_transcript.delta"));
        Assert.That(body["triggers"]![0]!["decisions"]!["model"], Is.Null);
        Assert.That(body["client"]!["data_channel"]!["allowed_server_events"]![0]!["type"]!.ToString(), Is.EqualTo("decision.event"));
        var snapshot = (OpenAiLiveSessionStartedEvent)OpenAiLiveEventParser.Parse(new JObject
        {
            ["type"] = "session.started", ["session"] = body
        }.ToString());
        Assert.That(snapshot.Session!.Delegation!.Decisions!.Questions![1], Is.TypeOf<OpenAiDecisionChoice>());
        Assert.That(snapshot.Session.Triggers![0].Decisions.Questions[0], Is.TypeOf<OpenAiDecisionPredicate>());

        JObject update = JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveClientEvents.DecisionsUpdate(new() { Questions = Questions() }, "update-1"), Settings));
        Assert.That(update["session"]!["delegation"]!["decisions"]!["questions"]!.Count(), Is.EqualTo(3));
        JObject append = JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveClientEvents.DecisionItemCreate(OpenAiLiveResponseItems.UserText("State changed"), "state-1"), Settings));
        Assert.That(append["type"]!.ToString(), Is.EqualTo("decision.item.create"));
        Assert.That(append["item"]!["content"]![0]!["text"]!.ToString(), Is.EqualTo("State changed"));
    }

    [Test]
    public async Task LiveDecisionEvent_ParsesRefusalsTypedValuesAndSourceIdentityAndDispatches()
    {
        const string json = """
            {"type":"decision.event","delegation_id":null,"trigger_id":"trg-1",
            "source_event_type":"session.input_transcript.delta","source_event_id":"delta-42",
            "event":{"type":"decision.completed","decision":{"answers":[
              {"type":"predicate","name":"damaged","probability":0.92},
              {"type":"choice","name":"route","choice":true,"confidence":0.9,"probabilities":[{"value":true,"probability":0.9},{"value":"true","probability":0.1}]},
              {"type":"refusal","name":"severity"}]}}}
            """;
        var evt = (OpenAiLiveDecisionEvent)OpenAiLiveEventParser.Parse(json);
        Assert.That(evt.SourceEventId, Is.EqualTo("delta-42"));
        Assert.That(evt.TriggerId, Is.EqualTo("trg-1"));
        Assert.That(evt.Event!.Decision!.GetAnswer("damaged")!.Probability, Is.EqualTo(0.92));
        Assert.That(evt.Event.Decision.GetAnswer("route")!.Choice!.Type, Is.EqualTo(JTokenType.Boolean));
        Assert.That(evt.Event.Decision.GetAnswer("severity")!.IsRefusal, Is.True);
        OpenAiLiveServerEvent? received = null;
        await new OpenAiLiveEventHandler { OnDecisionEvent = e => { received = e; return ValueTask.CompletedTask; } }.DispatchAsync(evt);
        Assert.That(received, Is.SameAs(evt));
        Assert.That(OpenAiLiveEventParser.Parse("""{"type":"decision.item.created","item":{"id":"item-1"}}"""), Is.TypeOf<OpenAiLiveDecisionItemCreatedEvent>());
    }

    [Test]
    public void SavedVoice_RoundTripsAnIdObjectAndNewBuiltinVoicesRemainStrings()
    {
        OpenAiLiveAudioOutput voice = new() { Voice = OpenAiLiveVoice.Marin, CustomVoice = "voice_123" };
        string json = JsonConvert.SerializeObject(voice, Settings);
        Assert.That(JObject.Parse(json)["voice"]!["id"]!.ToString(), Is.EqualTo("voice_123"));
        Assert.That(JsonConvert.DeserializeObject<OpenAiLiveAudioOutput>(json)!.CustomVoice, Is.EqualTo("voice_123"));
        foreach (var name in new[] { "nuri", "juni", "flitz", "brise", "sillage", "nira", "harema", "shida", "noeul" })
        {
            var builtin = JsonConvert.DeserializeObject<OpenAiLiveAudioOutput>(new JObject { ["voice"] = name }.ToString())!;
            Assert.That(builtin.Voice, Is.Not.Null, name);
            Assert.That(JObject.Parse(JsonConvert.SerializeObject(builtin, Settings))["voice"]!.ToString(), Is.EqualTo(name));
        }
    }

    [TestCase("fork")]
    [TestCase("accept")]
    [TestCase("reject")]
    [TestCase("refer")]
    [TestCase("hangup")]
    public async Task LiveRestActions_ResolveAbsoluteProviderUrls(string action)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        string prefix = $"http://127.0.0.1:{port}/";
        using HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();
        Task<HttpListenerContext> incoming = listener.GetContextAsync();
        TornadoApi api = new(new Uri(prefix), "test-key", LLmProviders.OpenAi);
        OpenAiLiveEndpoint live = api.Live.OpenAi;
        Task operation = action switch
        {
            "fork" => live.ForkWebRtcAsync("session-1", "sdp-offer", cancellationToken: timeout.Token),
            "accept" => live.AcceptAsync("session-1", OpenAiLiveSessionConfig.ForSipAccept(), timeout.Token),
            "reject" => live.RejectAsync("session-1", cancellationToken: timeout.Token),
            "refer" => live.ReferAsync("session-1", "tel:+420123456789", timeout.Token),
            _ => live.HangupAsync("session-1", timeout.Token)
        };
        HttpListenerContext context = await incoming.WaitAsync(timeout.Token);
        Assert.That(context.Request.Url!.AbsolutePath, Does.EndWith($"/live/sessions/session-1/{action}"));
        Assert.That(context.Request.Headers["Authorization"], Is.EqualTo("Bearer test-key"));
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{}"), timeout.Token);
        context.Response.Close();
        await operation.WaitAsync(timeout.Token);
    }

    [TestCase(false, 200)]
    [TestCase(true, 200)]
    [TestCase(false, 400)]
    public async Task PublicEndpoints_SendOfficialPathAuthAndPayloadAndReadResponses(bool voice, int status)
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
        Task<HttpListenerContext> incoming = listener.GetContextAsync();
        TornadoApi api = new(new Uri(prefix), "test-key", LLmProviders.OpenAi);
        Task operation = voice ? CreateVoice() : CreateDecision();
        HttpListenerContext context = await incoming.WaitAsync(timeout.Token);
        using StreamReader reader = new(context.Request.InputStream);
        JObject body = JObject.Parse(await reader.ReadToEndAsync(timeout.Token));
        Assert.That(context.Request.Headers["Authorization"], Is.EqualTo("Bearer test-key"));
        Assert.That(context.Request.Url!.AbsolutePath, Does.EndWith(voice ? "/audio/voices" : "/decisions"));
        Assert.That(body[voice ? "type" : "model"]!.ToString(), Is.EqualTo(voice ? "prompt" : "gpt-6-luna"));
        string response = status != 200 ? """{"error":{"message":"invalid input","type":"invalid_request_error"}}""" : voice
            ? """{"object":"audio.voice","id":"voice_123","name":"Support","type":"prompt","prompt":"Warm adult voice","created_at":1792500000}"""
            : """{"model":"gpt-6-luna","answers":[{"type":"predicate","name":"damaged","probability":0.92}],"usage":{"input_tokens":42,"output_tokens":0,"total_tokens":42}}""";
        byte[] bytes = Encoding.UTF8.GetBytes(response);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(bytes, timeout.Token);
        context.Response.Close();
        await operation.WaitAsync(timeout.Token);

        async Task CreateVoice()
        {
            var result = await api.Audio.OpenAiVoices.CreateFromPromptAsync(new() { Name = "Support", Prompt = "Warm adult voice", ScriptHint = "Hello, how can I help today?" }, timeout.Token);
            Assert.That(result.Data!.Id, Is.EqualTo("voice_123"));
            Assert.That(result.Data.CreatedAt, Is.EqualTo(1792500000));
        }
        async Task CreateDecision()
        {
            var result = await api.Decision.OpenAi.CreateSafeAsync(new() { Input = "Broken screen", Questions = Questions() }, timeout.Token);
            Assert.That((int)result.Code, Is.EqualTo(status));
            if (status == 200) Assert.That(result.Data!.GetAnswer("damaged")!.Probability, Is.EqualTo(0.92));
            else Assert.That(result.Ok, Is.False);
        }
    }
}
