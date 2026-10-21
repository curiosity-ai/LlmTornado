using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Live.OpenAi;

/// <summary>
/// Request body for <c>POST /v1/live/sessions</c> (WebRTC) and session fork.
/// </summary>
public class OpenAiLiveCreateRequest
{
    [JsonProperty("session")]
    public OpenAiLiveSessionConfig? Session { get; set; }

    [JsonProperty("transport")]
    public OpenAiLiveTransport? Transport { get; set; }

    /// <summary>
    /// Frontend client permissions. WebRTC only; preserved on WebRTC forks unless overridden.
    /// </summary>
    [JsonProperty("permissions")]
    public OpenAiLivePermissions? Permissions { get; set; }

    public static OpenAiLiveCreateRequest WebRtc(OpenAiLiveSessionConfig session, string sdpOffer, OpenAiLivePermissions? permissions = null)
    {
        return new OpenAiLiveCreateRequest
        {
            Session = session,
            Transport = OpenAiLiveTransport.WebRtc(sdpOffer),
            Permissions = permissions
        };
    }
}

/// <summary>
/// Response from WebRTC create or fork: session id plus SDP answer.
/// </summary>
public class OpenAiLiveCreateResponse
{
    [JsonProperty("session")]
    public OpenAiLiveSessionResource? Session { get; set; }

    [JsonProperty("transport")]
    public OpenAiLiveTransport? Transport { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Transport payload. WebRTC uses <c>type: webrtc</c> and an SDP blob.
/// </summary>
public class OpenAiLiveTransport
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("sdp")]
    public string? Sdp { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }

    public static OpenAiLiveTransport WebRtc(string sdp)
    {
        return new OpenAiLiveTransport { Type = "webrtc", Sdp = sdp };
    }
}

/// <summary>
/// Frontend permission bag. The official schema is additive; unknown fields are preserved.
/// </summary>
public class OpenAiLivePermissions
{
    [JsonProperty("client")]
    public OpenAiLiveClientPermissions? Client { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Data-channel / frontend permission flags.
/// </summary>
public class OpenAiLiveClientPermissions
{
    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// SIP accept body: <c>{ "session": { "type": "live", ... } }</c>.
/// </summary>
public class OpenAiLiveAcceptRequest
{
    [JsonProperty("session")]
    public OpenAiLiveSessionConfig Session { get; set; } = new OpenAiLiveSessionConfig();

    public static OpenAiLiveAcceptRequest Create(OpenAiLiveSessionConfig session)
    {
        if (string.IsNullOrEmpty(session.Type))
        {
            session.Type = "live";
        }

        return new OpenAiLiveAcceptRequest { Session = session };
    }
}

/// <summary>
/// SIP reject body. Status must be an integer from 300 through 699. Default when omitted is 603.
/// </summary>
public class OpenAiLiveRejectRequest
{
    [JsonProperty("status_code")]
    public int? StatusCode { get; set; }

    public static OpenAiLiveRejectRequest Busy() => new OpenAiLiveRejectRequest { StatusCode = 486 };

    public static OpenAiLiveRejectRequest Decline() => new OpenAiLiveRejectRequest { StatusCode = 603 };
}

/// <summary>
/// SIP REFER body.
/// </summary>
public class OpenAiLiveReferRequest
{
    [JsonProperty("target_uri")]
    public string TargetUri { get; set; } = string.Empty;

    public static OpenAiLiveReferRequest To(string targetUri)
    {
        return new OpenAiLiveReferRequest { TargetUri = targetUri };
    }
}

/// <summary>
/// Empty success body for hangup / accept / reject / refer.
/// </summary>
public class OpenAiLiveEmptyResponse
{
    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Inbound GPT-Live webhook (<c>live.transport.incoming</c> or deprecated <c>live.call.incoming</c>).
/// </summary>
public class OpenAiLiveIncomingWebhook
{
    [JsonProperty("object")]
    public string? Object { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("created_at")]
    public long? CreatedAt { get; set; }

    [JsonProperty("data")]
    public OpenAiLiveIncomingWebhookData? Data { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }

    [JsonIgnore]
    public bool IsSip =>
        string.Equals(Data?.Type, "sip", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Type, OpenAiLiveEventTypes.WebhookTransportIncoming, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Type, OpenAiLiveEventTypes.WebhookCallIncoming, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Session id for Live call-control APIs. Prefer <c>data.session_id</c>; fall back to legacy <c>data.call_id</c>.
    /// </summary>
    [JsonIgnore]
    public string? SessionId => Data?.SessionId ?? Data?.CallId;

    public static OpenAiLiveIncomingWebhook? Parse(string json)
    {
        return JsonConvert.DeserializeObject<OpenAiLiveIncomingWebhook>(json);
    }
}

/// <summary>
/// Webhook payload identifying an inbound SIP call.
/// </summary>
public class OpenAiLiveIncomingWebhookData
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("session_id")]
    public string? SessionId { get; set; }

    [JsonProperty("call_id")]
    public string? CallId { get; set; }

    [JsonProperty("sip_headers")]
    public List<OpenAiLiveSipHeader>? SipHeaders { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }
}

/// <summary>
/// Untrusted SIP header from an inbound webhook.
/// </summary>
public class OpenAiLiveSipHeader
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("value")]
    public string? Value { get; set; }
}

/// <summary>Capabilities for a WebRTC frontend attached to a Live session.</summary>
public class OpenAiLiveClientConfig
{
    [JsonProperty("data_channel")]
    public OpenAiLiveDataChannelConfig? DataChannel { get; set; }
}

/// <summary>Client and server event permissions. Omission permits all; an empty list permits none.</summary>
public class OpenAiLiveDataChannelConfig
{
    /// <summary>"all" or a list of client event type names.</summary>
    [JsonProperty("allowed_client_events", NullValueHandling = NullValueHandling.Ignore)]
    public object? AllowedClientEvents { get; set; }

    /// <summary>"all" or a list of OpenAiLiveServerEventSelector entries.</summary>
    [JsonProperty("allowed_server_events", NullValueHandling = NullValueHandling.Ignore)]
    public object? AllowedServerEvents { get; set; }
}

/// <summary>Selects one outer Live event, optionally narrowing a response.event to a nested Responses event.</summary>
public class OpenAiLiveServerEventSelector
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Required only for type response.event.</summary>
    [JsonProperty("response_event", NullValueHandling = NullValueHandling.Ignore)]
    public string? ResponseEvent { get; set; }
}
