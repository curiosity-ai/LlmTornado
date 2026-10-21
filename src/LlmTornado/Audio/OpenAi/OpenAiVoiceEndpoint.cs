using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;

namespace LlmTornado.Audio.OpenAi;

/// <summary>OpenAI prompt-to-voice creation. Saved voices can be reused in GPT-Live sessions.</summary>
public class OpenAiVoiceEndpoint : EndpointBase
{
    internal OpenAiVoiceEndpoint(TornadoApi api) : base(api) { }
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Audio;

    /// <summary>Creates and saves a voice from a text description.</summary>
    public Task<HttpCallResult<OpenAiVoice>> CreateFromPromptAsync(OpenAiVoicePromptRequest request, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<OpenAiVoice>(provider, Endpoint, provider.ApiUrl(Endpoint, "/voices"),
            postData: JsonConvert.SerializeObject(request, NullSettings), ct: cancellationToken);
    }
}

/// <summary>A prompt-generated voice request. Creation returns metadata rather than audio.</summary>
public class OpenAiVoicePromptRequest
{
    [JsonProperty("type")]
    public string Type => "prompt";

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of the voice's vocal qualities.</summary>
    [JsonProperty("prompt")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>auto (default) or a supported creation snapshot such as 2026-10-08.</summary>
    [JsonProperty("model")]
    public string Model { get; set; } = "auto";

    /// <summary>Optional example speech script, up to 2,000 characters.</summary>
    [JsonProperty("script_hint", NullValueHandling = NullValueHandling.Ignore)]
    public string? ScriptHint { get; set; }
}

/// <summary>Saved voice metadata. Use Id as the GPT-Live custom voice ID.</summary>
public class OpenAiVoice : ApiResultBase
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("object")]
    public string? Object { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("prompt")]
    public string? Prompt { get; set; }
}
