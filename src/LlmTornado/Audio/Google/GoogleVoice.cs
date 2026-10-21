using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using LlmTornado.Interactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.Audio.Google;

/// <summary>Custom voice creation request. Prompted voices require Store=true.</summary>
public class GoogleVoiceCreateRequest
{
    /// <summary>Persist the voice in the project. Set false for a stateless replicated voice key.</summary>
    [JsonProperty("store")]
    public bool Store { get; set; } = true;

    [JsonProperty("voice")]
    public GoogleVoice Voice { get; set; } = new();
}

/// <summary>A custom or prebuilt Gemini voice and its discovery metadata.</summary>
public class GoogleVoice : ApiResultBase
{
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string? Id { get; set; }

    /// <summary>Client-managed stateless replication key, returned when Store=false.</summary>
    [JsonProperty("key", NullValueHandling = NullValueHandling.Ignore)]
    public string? Key { get; set; }

    [JsonProperty("type")]
    public GoogleVoiceType Type { get; set; }

    [JsonProperty("display_name", NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayName { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("language_code", NullValueHandling = NullValueHandling.Ignore)]
    public string? LanguageCode { get; set; }

    [JsonProperty("region_code", NullValueHandling = NullValueHandling.Ignore)]
    public string? RegionCode { get; set; }

    [JsonProperty("accent", NullValueHandling = NullValueHandling.Ignore)]
    public string? Accent { get; set; }

    [JsonProperty("gender", NullValueHandling = NullValueHandling.Ignore)]
    public string? Gender { get; set; }

    [JsonProperty("persona", NullValueHandling = NullValueHandling.Ignore)]
    public string? Persona { get; set; }

    [JsonProperty("context", NullValueHandling = NullValueHandling.Ignore)]
    public string? Context { get; set; }

    /// <summary>low, medium, or high.</summary>
    [JsonProperty("pitch", NullValueHandling = NullValueHandling.Ignore)]
    public string? Pitch { get; set; }

    [JsonProperty("prompted", NullValueHandling = NullValueHandling.Ignore)]
    public GooglePromptedVoice? Prompted { get; set; }

    [JsonProperty("replicated", NullValueHandling = NullValueHandling.Ignore)]
    public GoogleReplicatedVoice? Replicated { get; set; }

    [JsonProperty("sample_audio", NullValueHandling = NullValueHandling.Ignore)]
    public GoogleVoiceAudio? SampleAudio { get; set; }

    [JsonProperty("expire_time", NullValueHandling = NullValueHandling.Ignore)]
    public DateTimeOffset? ExpireTime { get; set; }

    [JsonProperty("usage", NullValueHandling = NullValueHandling.Ignore)]
    public InteractionUsage? Usage { get; set; }
}

/// <summary>Parameters for voice design or remixing an existing voice.</summary>
public class GooglePromptedVoice
{
    [JsonProperty("input")]
    public string Input { get; set; } = string.Empty;

    [JsonProperty("base_voice", NullValueHandling = NullValueHandling.Ignore)]
    public string? BaseVoice { get; set; }
}

/// <summary>Reference and consent recordings from the same speaker.</summary>
public class GoogleReplicatedVoice
{
    [JsonProperty("source_audio")]
    public GoogleVoiceAudio SourceAudio { get; set; } = new();

    [JsonProperty("consent_audio")]
    public GoogleVoiceAudio ConsentAudio { get; set; } = new();
}

/// <summary>Inline audio used for voice creation or returned as a design preview.</summary>
public class GoogleVoiceAudio
{
    [JsonProperty("mime_type")]
    public string MimeType { get; set; } = "audio/wav";

    /// <summary>Base64-encoded audio bytes.</summary>
    [JsonProperty("data")]
    public string Data { get; set; } = string.Empty;
}

[JsonConverter(typeof(StringEnumConverter))]
public enum GoogleVoiceType
{
    [EnumMember(Value = "prompted")] Prompted,
    [EnumMember(Value = "replicated")] Replicated,
    [EnumMember(Value = "prebuilt")] Prebuilt
}

/// <summary>Voice library page. Pass NextPageToken with the same filters to retrieve the next page.</summary>
public class GoogleVoiceList : ApiResultBase
{
    [JsonProperty("voices")]
    public List<GoogleVoice> Voices { get; set; } = [];

    [JsonProperty("next_page_token")]
    public string? NextPageToken { get; set; }
}

/// <summary>Filters for the voice library. Values within one field are ORed; different fields are ANDed.</summary>
public class GoogleVoiceListRequest
{
    public int? PageSize { get; set; }
    public string? PageToken { get; set; }
    public string? Search { get; set; }
    public List<string>? LanguageCode { get; set; }
    public List<string>? RegionCode { get; set; }
    public List<string>? Accent { get; set; }
    public List<string>? Persona { get; set; }
    public List<string>? Context { get; set; }
    public List<string>? Gender { get; set; }
    public List<string>? Pitch { get; set; }
    public List<string>? Type { get; set; }
}

/// <summary>Successful voice deletion returns an empty resource.</summary>
public class GoogleVoiceDeleteResult : ApiResultBase { }
