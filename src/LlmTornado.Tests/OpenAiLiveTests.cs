using System;
using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Live.OpenAi;
using LlmTornado.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class OpenAiLiveTests
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    [Test]
    public void TornadoApi_ExposesLiveAndOpenAiFacade()
    {
        TornadoApi api = new TornadoApi("test-key");
        Assert.That(api.Live, Is.Not.Null);
        Assert.That(api.Live.OpenAi, Is.Not.Null);
        Assert.That(api.Live.Api, Is.SameAs(api));
    }

    [Test]
    public void OpenAi_UrlFragment_IsLiveSessions()
    {
        Assert.That(OpenAiEndpointProvider.GetEndpointUrlFragment(CapabilityEndpoints.Live), Is.EqualTo("live/sessions"));
    }

    [Test]
    public void RestUrl_CreateAndActions()
    {
        TornadoApi api = new TornadoApi("test-key");
        Assert.That(api.Live.OpenAi.ResolveRestUrl(), Does.EndWith("/v1/live/sessions"));
        Assert.That(api.Live.OpenAi.ResolveRestUrl("/sess_1/fork"), Does.EndWith("/v1/live/sessions/sess_1/fork"));
        Assert.That(api.Live.OpenAi.ResolveRestUrl("/sess_1/content"), Does.EndWith("/v1/live/sessions/sess_1/content"));
        Assert.That(api.Live.OpenAi.ResolveRestUrl("/sess_1/accept"), Does.EndWith("/v1/live/sessions/sess_1/accept"));
        Assert.That(api.Live.OpenAi.ResolveRestUrl("/sess_1/reject"), Does.EndWith("/v1/live/sessions/sess_1/reject"));
        Assert.That(api.Live.OpenAi.ResolveRestUrl("/sess_1/refer"), Does.EndWith("/v1/live/sessions/sess_1/refer"));
        Assert.That(api.Live.OpenAi.ResolveRestUrl("/sess_1/hangup"), Does.EndWith("/v1/live/sessions/sess_1/hangup"));
    }

    [Test]
    public void ConnectOptions_BuildOfficialWebSocketUrls()
    {
        Assert.That(OpenAiLiveConnectOptions.Primary(OpenAiLiveSessionConfig.ForWebSocket()).BuildWebSocketUri().ToString(),
            Is.EqualTo("wss://api.openai.com/v1/live/sessions"));

        Assert.That(OpenAiLiveConnectOptions.Sideband("live_abc").BuildWebSocketUri().ToString(),
            Is.EqualTo("wss://api.openai.com/v1/live/sessions/live_abc/attach"));

        Assert.That(OpenAiLiveConnectOptions.Fork("live_src").BuildWebSocketUri().ToString(),
            Is.EqualTo("wss://api.openai.com/v1/live/sessions/live_src/fork"));
    }

    [Test]
    public void SidebandAndFork_RequireSessionId()
    {
        Assert.Throws<InvalidOperationException>(() => new OpenAiLiveConnectOptions { Kind = OpenAiLiveConnectKind.Sideband }.BuildWebSocketUri());
        Assert.That(OpenAiLiveConnectOptions.Sideband("id").ShouldSendSessionStart, Is.False);
        Assert.That(OpenAiLiveConnectOptions.Fork("id").ShouldSendSessionStart, Is.True);
        Assert.That(OpenAiLiveConnectOptions.Primary(new OpenAiLiveSessionConfig()).ShouldSendSessionStart, Is.True);
    }

    [Test]
    public void SessionConfig_WebSocketDefault_SerializesOfficialShape()
    {
        OpenAiLiveSessionConfig config = OpenAiLiveSessionConfig.ForWebSocket(
            "Be concise.",
            OpenAiLiveVoice.Marin,
            OpenAiLiveAudioFormat.Pcm24Khz(),
            OpenAiLiveDelegationConfig.ResponsesBackend("gpt-5.6-luna", "Search when needed",
            [
                OpenAiLiveDelegationTool.WebSearch(),
                OpenAiLiveDelegationTool.Function("get_weather", "Weather lookup", new
                {
                    type = "object",
                    properties = new { location = new { type = "string" } },
                    required = new[] { "location" }
                })
            ]));

        JObject json = JObject.Parse(JsonConvert.SerializeObject(config, Settings));
        Assert.That(json["model"]!.ToString(), Is.EqualTo("gpt-live-1"));
        Assert.That(json["audio"]!["format"]!["type"]!.ToString(), Is.EqualTo("audio/pcm"));
        Assert.That(json["audio"]!["format"]!["rate"]!.Value<int>(), Is.EqualTo(24000));
        Assert.That(json["audio"]!["output"]!["voice"]!.ToString(), Is.EqualTo("marin"));
        Assert.That(json["delegation"]!["type"]!.ToString(), Is.EqualTo("responses"));
        Assert.That(json["delegation"]!["responses"]!["model"]!.ToString(), Is.EqualTo("gpt-5.6-luna"));
        Assert.That(json["delegation"]!["responses"]!["tools"]![0]!["type"]!.ToString(), Is.EqualTo("web_search"));
        Assert.That(json["delegation"]!["responses"]!["tools"]![1]!["name"]!.ToString(), Is.EqualTo("get_weather"));
        Assert.That(json["audio"]!["format"]!["type"]!.ToString(), Is.Not.EqualTo("audio.format"));
    }

    [Test]
    public void SessionConfig_WebRtc_OmitsAudioFormat()
    {
        JObject json = JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveSessionConfig.ForWebRtc("Hello", OpenAiLiveVoice.Quartz), Settings));
        Assert.That(json["audio"]!["format"], Is.Null);
        Assert.That(json["audio"]!["output"]!["voice"]!.ToString(), Is.EqualTo("quartz"));
    }

    [Test]
    public void SessionConfig_SipAccept_IncludesLiveType()
    {
        JObject json = JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveSessionConfig.ForSipAccept("Support"), Settings));
        Assert.That(json["type"]!.ToString(), Is.EqualTo("live"));
        Assert.That(json["audio"]!["format"], Is.Null);
    }

    [Test]
    public void SessionConfig_HistoryAndStore()
    {
        OpenAiLiveSessionConfig config = OpenAiLiveSessionConfig.ForWebSocket()
            .WithModel(ChatModel.OpenAi.Realtime.Live1);
        config.Store = true;
        config.Input =
        [
            OpenAiLiveInputMessage.User("I need help with my order."),
            OpenAiLiveInputMessage.Assistant("What is the order number?"),
            OpenAiLiveInputMessage.Developer("Never reveal secrets.")
        ];

        JObject json = JObject.Parse(JsonConvert.SerializeObject(config, Settings));
        Assert.That(json["store"]!.Value<bool>(), Is.True);
        Assert.That(json["input"]!.Count(), Is.EqualTo(3));
        Assert.That(json["input"]![0]!["content"]![0]!["type"]!.ToString(), Is.EqualTo("input_text"));
        Assert.That(json["input"]![1]!["content"]![0]!["type"]!.ToString(), Is.EqualTo("output_text"));
    }

    [Test]
    public void AllVoices_SerializeToApiNames()
    {
        string[] expected =
        [
            "alloy", "ash", "ballad", "cedar", "coral", "echo", "sage", "shimmer", "verse",
            "marin", "quartz", "ripple", "vesper", "willow", "stone",
            "gleam", "meridian", "bossa", "tempo", "beacon", "delta", "cinder", "nuri", "juni", "flitz", "brise", "sillage", "nira", "harema", "shida", "noeul"
        ];

        OpenAiLiveVoice[] voices = Enum.GetValues<OpenAiLiveVoice>();
        Assert.That(voices, Has.Length.EqualTo(expected.Length));

        foreach (OpenAiLiveVoice voice in voices)
        {
            OpenAiLiveAudioOutput output = new OpenAiLiveAudioOutput { Voice = voice };
            JObject json = JObject.Parse(JsonConvert.SerializeObject(output, Settings));
            Assert.That(expected, Does.Contain(json["voice"]!.ToString()));
        }
    }

    [Test]
    public void AudioFormats_MatchOfficialCodecs()
    {
        Assert.That(JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveAudioFormat.Pcm16Khz(), Settings))["rate"]!.Value<int>(), Is.EqualTo(16000));
        Assert.That(JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveAudioFormat.G711Ulaw(), Settings))["type"]!.ToString(), Is.EqualTo("audio/pcmu"));
        Assert.That(JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveAudioFormat.G711Alaw(), Settings))["type"]!.ToString(), Is.EqualTo("audio/pcma"));
    }

    [Test]
    public void ClientEvents_SerializeAllCommandTypes()
    {
        AssertEvent(OpenAiLiveClientEvents.SessionStart(OpenAiLiveSessionConfig.ForForkInherit(), "event_start"), "session.start");
        AssertEvent(OpenAiLiveClientEvents.SessionUpdate(new OpenAiLiveResponsesDelegation { Model = "gpt-5.6-terra" }, "upd"), "session.update");
        AssertEvent(OpenAiLiveClientEvents.InputAudioAppend("YQ==", "a1"), "session.input_audio.append");
        AssertEvent(OpenAiLiveClientEvents.InputAudioMute("m1"), "session.input_audio.mute");
        AssertEvent(OpenAiLiveClientEvents.InputAudioUnmute("u1"), "session.input_audio.unmute");
        AssertEvent(OpenAiLiveClientEvents.InstructionsAppend("Greet now.", null, "i1"), "session.instructions.append");
        AssertEvent(OpenAiLiveClientEvents.ThinkingAppend("Lookup running.", "item_1", "t1"), "session.thinking.append");
        AssertEvent(OpenAiLiveClientEvents.CommentaryAppend("It shipped.", "item_1", "c1"), "session.commentary.append");
        AssertEvent(OpenAiLiveClientEvents.ResponseItemCreate(OpenAiLiveResponseItems.FunctionCallOutput("call_1", "{}"), "r1"), "response.item.create");
        AssertEvent(OpenAiLiveClientEvents.ResponseCreate("go"), "response.create");
        AssertEvent(OpenAiLiveClientEvents.SessionClose("bye"), "session.close");
    }

    [Test]
    public void ContextAppend_IncludesNullDelegationId()
    {
        string json = JsonConvert.SerializeObject(OpenAiLiveClientEvents.InstructionsAppend("Stop.", null, "g1"), Settings);
        JObject obj = JObject.Parse(json);
        Assert.That(obj.ContainsKey("delegation_id"), Is.True);
        Assert.That(obj["delegation_id"]!.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void SessionUpdate_ReplacesDelegationObject()
    {
        OpenAiLiveSessionUpdateEvent evt = OpenAiLiveClientEvents.SessionUpdate(new OpenAiLiveResponsesDelegation
        {
            Model = "gpt-5.6-terra",
            ToolChoice = "auto",
            ParallelToolCalls = true,
            MaxOutputTokens = 2048,
            ServiceTier = ChatRequestServiceTiers.Priority,
            Reasoning = new ReasoningConfiguration(ResponseReasoningEfforts.Medium, ResponseReasoningSummaries.Auto),
            Text = OpenAiLiveTextConfig.Low()
        });

        JObject json = JObject.Parse(JsonConvert.SerializeObject(evt, Settings));
        Assert.That(json["session"]!["delegation"]!["type"]!.ToString(), Is.EqualTo("responses"));
        Assert.That(json["session"]!["delegation"]!["responses"]!["service_tier"]!.ToString(), Is.EqualTo("priority"));
        Assert.That(json["session"]!["delegation"]!["responses"]!["reasoning"]!["effort"]!.ToString(), Is.EqualTo("medium"));
        Assert.That(json["session"]!["delegation"]!["responses"]!["text"]!["verbosity"]!.ToString(), Is.EqualTo("low"));
        Assert.That(json["session"]!["model"], Is.Null);
    }

    [Test]
    public void Parser_DispatchesOfficialServerEvents()
    {
        OpenAiLiveServerEvent started = OpenAiLiveEventParser.Parse("""
            {"type":"session.started","session":{"id":"sess_123","expires_at":1782518400,"model":"gpt-live-1","instructions":"Be concise.","audio":{"output":{"voice":"marin"}},"delegation":{"type":"client"}}}
            """);
        Assert.That(started, Is.TypeOf<OpenAiLiveSessionStartedEvent>());
        Assert.That(((OpenAiLiveSessionStartedEvent)started).Session!.Id, Is.EqualTo("sess_123"));
        Assert.That(((OpenAiLiveSessionStartedEvent)started).Session!.Audio!.Output!.Voice, Is.EqualTo(OpenAiLiveVoice.Marin));

        OpenAiLiveServerEvent audio = OpenAiLiveEventParser.Parse("""
            {"type":"session.output_audio.delta","delta":"YQ==","start_ms":0,"end_ms":100}
            """);
        Assert.That(((OpenAiLiveOutputAudioDeltaEvent)audio).DecodeAudio(), Is.EqualTo(new byte[] { 0x61 }));

        OpenAiLiveServerEvent transcript = OpenAiLiveEventParser.Parse("""
            {"type":"session.input_transcript.delta","delta":"What is","start_ms":1000,"end_ms":1200}
            """);
        Assert.That(((OpenAiLiveInputTranscriptDeltaEvent)transcript).Delta, Is.EqualTo("What is"));

        OpenAiLiveServerEvent outputTranscript = OpenAiLiveEventParser.Parse("""{"type":"session.output_transcript.delta","delta":"Sure"}""");
        Assert.That(outputTranscript, Is.TypeOf<OpenAiLiveOutputTranscriptDeltaEvent>());

        OpenAiLiveServerEvent reflected = OpenAiLiveEventParser.Parse("""{"type":"session.input_audio.append","audio":"YQ=="}""");
        Assert.That(reflected, Is.TypeOf<OpenAiLiveInputAudioReflectedEvent>());

        OpenAiLiveServerEvent appended = OpenAiLiveEventParser.Parse("""{"type":"session.thinking.appended","client_event_id":"context_1","start_ms":10,"end_ms":20}""");
        Assert.That(((OpenAiLiveThinkingAppendedEvent)appended).ClientEventId, Is.EqualTo("context_1"));

        OpenAiLiveServerEvent muted = OpenAiLiveEventParser.Parse("""{"type":"session.input_audio.muted","client_event_id":"mute_1"}""");
        Assert.That(muted, Is.TypeOf<OpenAiLiveInputAudioMutedEvent>());

        OpenAiLiveServerEvent unmuted = OpenAiLiveEventParser.Parse("""{"type":"session.input_audio.unmuted"}""");
        Assert.That(unmuted, Is.TypeOf<OpenAiLiveInputAudioUnmutedEvent>());

        OpenAiLiveServerEvent delegation = OpenAiLiveEventParser.Parse("""
            {"type":"session.delegation.created","offset_ms":1000,"delegation":{"id":"item_9","type":"delegation","target":"client"}}
            """);
        Assert.That(((OpenAiLiveDelegationCreatedEvent)delegation).Delegation!.Id, Is.EqualTo("item_9"));
        Assert.That(((OpenAiLiveDelegationCreatedEvent)delegation).Delegation!.Target, Is.EqualTo("client"));

        OpenAiLiveServerEvent response = OpenAiLiveEventParser.Parse("""
            {"type":"response.event","delegation_id":"item_9","event":{"type":"response.output_text.delta","delta":"The forecast is"}}
            """);
        OpenAiLiveResponseEvent envelope = (OpenAiLiveResponseEvent)response;
        Assert.That(envelope.NestedType, Is.EqualTo("response.output_text.delta"));
        Assert.That(envelope.DelegationId, Is.EqualTo("item_9"));

        OpenAiLiveServerEvent usage = OpenAiLiveEventParser.Parse("""
            {"type":"session.usage.updated","usage":{"seconds":12},"context_window":{"usage_ratio":0.42}}
            """);
        Assert.That(((OpenAiLiveUsageUpdatedEvent)usage).Usage!.Seconds, Is.EqualTo(12));
        Assert.That(((OpenAiLiveUsageUpdatedEvent)usage).ContextWindow!.UsageRatio, Is.EqualTo(0.42).Within(0.001));

        OpenAiLiveServerEvent closed = OpenAiLiveEventParser.Parse("""
            {"type":"session.closed","reason":"close_requested","usage":{"seconds":128}}
            """);
        Assert.That(((OpenAiLiveSessionClosedEvent)closed).Reason, Is.EqualTo(OpenAiLiveCloseReason.CloseRequested));

        OpenAiLiveServerEvent error = OpenAiLiveEventParser.Parse("""
            {"type":"error","error":{"type":"invalid_request_error","code":"immutable_field_update","message":"no","param":"session.delegation.type","client_event_id":"event_update"}}
            """);
        Assert.That(((OpenAiLiveErrorEvent)error).Error!.Code, Is.EqualTo("immutable_field_update"));
        Assert.That(((OpenAiLiveErrorEvent)error).Error!.ClientEventId, Is.EqualTo("event_update"));

        OpenAiLiveServerEvent dtmf = OpenAiLiveEventParser.Parse("""{"type":"transport.dtmf.received","event":"#"}""");
        Assert.That(((OpenAiLiveDtmfReceivedEvent)dtmf).Event, Is.EqualTo("#"));

        OpenAiLiveServerEvent unknown = OpenAiLiveEventParser.Parse("""{"type":"future.event","extra":1}""");
        Assert.That(unknown, Is.TypeOf<OpenAiLiveServerEvent>());
        Assert.That(unknown.Type, Is.EqualTo("future.event"));
    }

    [Test]
    public async Task EventHandler_DispatchesTypedCallbacks()
    {
        List<string> seen = [];
        OpenAiLiveEventHandler handler = new OpenAiLiveEventHandler
        {
            OnEvent = evt =>
            {
                seen.Add("any:" + evt.Type);
                return ValueTask.CompletedTask;
            },
            OnSessionStarted = evt =>
            {
                seen.Add("started:" + evt.Session?.Id);
                return ValueTask.CompletedTask;
            },
            OnDelegationCreated = evt =>
            {
                seen.Add("delegation:" + evt.Delegation?.Id);
                return ValueTask.CompletedTask;
            }
        };

        await handler.DispatchAsync(OpenAiLiveEventParser.Parse("""{"type":"session.started","session":{"id":"sess_1"}}"""));
        await handler.DispatchAsync(OpenAiLiveEventParser.Parse("""{"type":"session.delegation.created","delegation":{"id":"item_1","target":"responses"}}"""));

        Assert.That(seen, Does.Contain("started:sess_1"));
        Assert.That(seen, Does.Contain("delegation:item_1"));
        Assert.That(seen.Count, Is.EqualTo(4));
    }

    [Test]
    public void WebRtcCreateRequest_SerializesSessionAndTransport()
    {
        OpenAiLiveCreateRequest request = OpenAiLiveCreateRequest.WebRtc(
            OpenAiLiveSessionConfig.ForWebRtc("Be concise."),
            "v=0\r\no=- 1 1 IN IP4 0.0.0.0\r\n");

        JObject json = JObject.Parse(JsonConvert.SerializeObject(request, Settings));
        Assert.That(json["transport"]!["type"]!.ToString(), Is.EqualTo("webrtc"));
        Assert.That(json["transport"]!["sdp"]!.ToString(), Does.StartWith("v=0"));
        Assert.That(json["session"]!["model"]!.ToString(), Is.EqualTo("gpt-live-1"));
        Assert.That(json["session"]!["audio"]!["format"], Is.Null);
    }

    [Test]
    public void SipBodies_Serialize()
    {
        JObject accept = JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveAcceptRequest.Create(OpenAiLiveSessionConfig.ForSipAccept()), Settings));
        Assert.That(accept["session"]!["type"]!.ToString(), Is.EqualTo("live"));

        JObject reject = JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveRejectRequest.Busy(), Settings));
        Assert.That(reject["status_code"]!.Value<int>(), Is.EqualTo(486));

        JObject refer = JObject.Parse(JsonConvert.SerializeObject(OpenAiLiveReferRequest.To("sip:agent@example.com"), Settings));
        Assert.That(refer["target_uri"]!.ToString(), Is.EqualTo("sip:agent@example.com"));
    }

    [Test]
    public void IncomingWebhook_ReadsSessionIdFromLiveAndLegacyShapes()
    {
        OpenAiLiveIncomingWebhook live = OpenAiLiveIncomingWebhook.Parse("""
            {"object":"event","id":"evt_1","type":"live.transport.incoming","created_at":1750287018,"data":{"type":"sip","session_id":"live_abc","sip_headers":[{"name":"From","value":"sip:+1@example.com"}]}}
            """)!;
        Assert.That(live.IsSip, Is.True);
        Assert.That(live.SessionId, Is.EqualTo("live_abc"));
        Assert.That(live.Data!.SipHeaders![0].Name, Is.EqualTo("From"));

        OpenAiLiveIncomingWebhook legacy = OpenAiLiveIncomingWebhook.Parse("""
            {"type":"live.call.incoming","data":{"call_id":"rtc_legacy"}}
            """)!;
        Assert.That(legacy.SessionId, Is.EqualTo("rtc_legacy"));
    }

    [Test]
    public void CloseReasons_AndDataChannelLabel()
    {
        Assert.That(OpenAiLiveEventTypes.WebRtcDataChannelLabel, Is.EqualTo("oai-events"));
        Assert.That(JsonConvert.DeserializeObject<OpenAiLiveSessionClosedEvent>("{\"type\":\"session.closed\",\"reason\":\"remote_hangup\"}")!.Reason, Is.EqualTo(OpenAiLiveCloseReason.RemoteHangup));
        Assert.That(JsonConvert.DeserializeObject<OpenAiLiveSessionClosedEvent>("{\"type\":\"session.closed\",\"reason\":\"connection_lost\"}")!.Reason, Is.EqualTo(OpenAiLiveCloseReason.ConnectionLost));
        Assert.That(JsonConvert.DeserializeObject<OpenAiLiveSessionClosedEvent>("{\"type\":\"session.closed\",\"reason\":\"expired\"}")!.Reason, Is.EqualTo(OpenAiLiveCloseReason.Expired));
        Assert.That(JsonConvert.DeserializeObject<OpenAiLiveSessionClosedEvent>("{\"type\":\"session.closed\",\"reason\":\"content\"}")!.Reason, Is.EqualTo(OpenAiLiveCloseReason.Content));
    }

    [Test]
    public void Live1_ModelIsRegistered()
    {
        Assert.That(ChatModel.OpenAi.Realtime.Live1.Name, Is.EqualTo("gpt-live-1"));
        Assert.That(ChatModel.OpenAi.Realtime.Live1.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Live));
    }

    [Test]
    public void CustomVoice_SerializesAsIdObject()
    {
        OpenAiLiveAudioOutput output = new OpenAiLiveAudioOutput { CustomVoice = "my-custom-voice" };
        JObject json = JObject.Parse(JsonConvert.SerializeObject(output, Settings));
        Assert.That(json["voice"]!["id"]!.ToString(), Is.EqualTo("my-custom-voice"));
    }

    [Test]
    public void TranscriptBuffer_PreservesFragmentsAndSpaces()
    {
        OpenAiLiveTranscriptBuffer buffer = new OpenAiLiveTranscriptBuffer();
        buffer.Apply(OpenAiLiveEventParser.Parse("""{"type":"session.input_transcript.delta","delta":"What is","start_ms":1000,"end_ms":1200}"""));
        buffer.Apply(OpenAiLiveEventParser.Parse("""{"type":"session.input_transcript.delta","delta":" the order","start_ms":1200,"end_ms":1400}"""));
        buffer.Apply(OpenAiLiveEventParser.Parse("""{"type":"session.output_transcript.delta","delta":"Sure"}"""));
        Assert.That(buffer.UserText, Is.EqualTo("What is the order"));
        Assert.That(buffer.AssistantText, Is.EqualTo("Sure"));
        Assert.That(buffer.User[0].StartMs, Is.EqualTo(1000));
    }

    [Test]
    public void AudioEncode_RejectsOddPcmLength()
    {
        Assert.Throws<ArgumentException>(() => OpenAiLiveAudio.Encode([1]));
        Assert.That(OpenAiLiveAudio.Encode([1, 2]), Is.EqualTo(Convert.ToBase64String(new byte[] { 1, 2 })));
        Assert.That(OpenAiLiveAudio.Encode([1], OpenAiLiveAudioFormat.G711Ulaw()), Is.EqualTo(Convert.ToBase64String(new byte[] { 1 })));
    }

    private static void AssertEvent(OpenAiLiveClientEvent evt, string type)
    {
        JObject json = JObject.Parse(JsonConvert.SerializeObject(evt, Settings));
        Assert.That(json["type"]!.ToString(), Is.EqualTo(type));
    }
}
