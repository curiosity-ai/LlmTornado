using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;

namespace LlmTornado.Audio.Google;

/// <summary>Gemini voice design, replication, and voice-library operations.</summary>
public class GoogleVoiceEndpoint : EndpointBase
{
    internal GoogleVoiceEndpoint(TornadoApi api) : base(api) { }
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Voices;

    /// <summary>Creates a prompted voice or replicates a speaker from reference and consent recordings.</summary>
    public Task<HttpCallResult<GoogleVoice>> CreateAsync(GoogleVoiceCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Voice.Type == GoogleVoiceType.Prebuilt)
            throw new ArgumentException("Only prompted or replicated voices can be created.", nameof(request));
        if (request.Voice.Type == GoogleVoiceType.Prompted && (!request.Store || string.IsNullOrWhiteSpace(request.Voice.Prompted?.Input)))
            throw new ArgumentException("Prompted voices require Store=true and a voice design prompt.", nameof(request));
        if (request.Voice.Type == GoogleVoiceType.Replicated &&
            (string.IsNullOrWhiteSpace(request.Voice.Replicated?.SourceAudio.Data) || string.IsNullOrWhiteSpace(request.Voice.Replicated?.ConsentAudio.Data)))
            throw new ArgumentException("Replicated voices require source and consent audio.", nameof(request));

        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Google);
        return HttpPost<GoogleVoice>(provider, Endpoint, postData: JsonConvert.SerializeObject(request, NullSettings), ct: cancellationToken);
    }

    /// <summary>Gets a stored custom voice, including the preview audio for a prompted voice.</summary>
    public Task<HttpCallResult<GoogleVoice>> GetAsync(string voiceId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Google);
        return HttpGet<GoogleVoice>(provider, Endpoint, provider.ApiUrl(Endpoint, VoicePath(voiceId)), ct: cancellationToken);
    }

    /// <summary>Deletes a stored custom voice.</summary>
    public Task<HttpCallResult<GoogleVoiceDeleteResult>> DeleteAsync(string voiceId, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Google);
        return HttpDelete<GoogleVoiceDeleteResult>(provider, Endpoint, provider.ApiUrl(Endpoint, VoicePath(voiceId)), ct: cancellationToken);
    }

    /// <summary>Lists stored custom voices and prebuilt voices, with pagination and discovery filters.</summary>
    public Task<HttpCallResult<GoogleVoiceList>> ListAsync(GoogleVoiceListRequest? request = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.Google);
        string url = provider.ApiUrl(Endpoint, null);
        if (request is not null)
        {
            StringBuilder query = new();
            Add("page_size", request.PageSize?.ToString(CultureInfo.InvariantCulture));
            Add("page_token", request.PageToken);
            Add("search", request.Search);
            AddMany("language_code", request.LanguageCode);
            AddMany("region_code", request.RegionCode);
            AddMany("accent", request.Accent);
            AddMany("persona", request.Persona);
            AddMany("context", request.Context);
            AddMany("gender", request.Gender);
            AddMany("pitch", request.Pitch);
            AddMany("type", request.Type);
            if (query.Length > 0) url += (url.Contains('?') ? "&" : "?") + query;

            void Add(string key, string? value)
            {
                if (value is null) return;
                if (query.Length > 0) query.Append('&');
                query.Append(key).Append('=').Append(Uri.EscapeDataString(value));
            }
            void AddMany(string key, List<string>? values)
            {
                if (values is null) return;
                foreach (string value in values) Add(key, value);
            }
        }
        return HttpGet<GoogleVoiceList>(provider, Endpoint, url, ct: cancellationToken);
    }

    private static string VoicePath(string voiceId)
    {
        if (voiceId.StartsWith("voices/", StringComparison.Ordinal)) voiceId = voiceId.Substring(7);
        if (string.IsNullOrWhiteSpace(voiceId)) throw new ArgumentException("A stored voice ID is required.", nameof(voiceId));
        return "/" + Uri.EscapeDataString(voiceId);
    }
}
