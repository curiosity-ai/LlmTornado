using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Audio.Google;
using LlmTornado.Code;
using LlmTornado.Interactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Audio.Vendors.Google;

internal static class VendorGoogleSpeechHandler
{
    internal static InteractionCreateRequest CreateRequest(SpeechRequest request, bool streaming = false)
    {
        if (request.Model.Name is not ("gemini-3.8-flash-tts" or "gemini-3.8-flash-lite-tts"))
            throw new ArgumentException("Gemini speech synthesis requires a supported TTS model.", nameof(request));

        List<InteractionContent> content = [];
        if (request.Google?.Turns is { Count: > 0 } turns)
        {
            foreach (GoogleSpeechTurn turn in turns)
                content.Add(Text(turn.Text, turn.Style ?? request.Instructions, turn.Speaker));
        }
        else
        {
            content.Add(Text(request.Input, request.Instructions, null));
        }

        string voice = request.Google?.Voice ?? request.VoiceId ?? (request.Voice.ToString() == "alloy" ? "Kore" : request.Voice.ToString());
        return new InteractionCreateRequest
        {
            Model = request.Model.Name,
            Input = InteractionInput.FromSteps(new InteractionStep { Type = "user_input", Content = content }),
            ResponseFormat = new InteractionResponseFormat
            {
                Type = "audio", Delivery = "inline", MimeType = MimeType(request.ResponseFormat),
                SampleRate = request.SampleRate, BitRate = request.Google?.BitRate
            },
            GenerationConfig = new InteractionGenerationConfig
            {
                SpeechConfig = request.Google?.Speakers is { Count: > 0 } speakers ? speakers :
                    [new InteractionSpeechConfig { Voice = voice, Language = request.Google?.Language }]
            },
            Store = false,
            Stream = streaming ? true : null
        };
    }

    private static InteractionContent Text(string text, string? style, string? speaker) => new()
    {
        Type = "text", Text = text,
        Annotations = style is not null || speaker is not null ? [InteractionAnnotation.Speech(style, speaker)] : null
    };

    private static string MimeType(SpeechResponseFormat format) => format.ToString() switch
    {
        "mp3" => "audio/mp3",
        "opus" => "audio/ogg_opus",
        "wav" => "audio/wav",
        "pcm" => "audio/l16",
        "alaw" => "audio/alaw",
        "mulaw" => "audio/mulaw",
        _ => throw new ArgumentException("Gemini TTS supports MP3, Opus, WAV, PCM, A-law, and mu-law output.", nameof(format))
    };

    internal static async Task<SpeechTtsResult?> Generate(SpeechRequest request, IEndpointProvider provider, EndpointBase endpoint)
    {
        InteractionCreateRequest body = CreateRequest(request);
        await using TornadoStreamRequest transport = await endpoint.HttpStreamingRequestData(provider, CapabilityEndpoints.Interactions,
            postData: body.Serialize(), requestObj: body, token: request.CancellationToken).ConfigureAwait(false);
        if (transport.Exception is not null) throw transport.Exception;
        if (transport.StreamReader is null || transport.Response is null) return null;

        using JsonTextReader reader = new(transport.StreamReader);
        JObject json = await JObject.LoadAsync(reader, request.CancellationToken).ConfigureAwait(false);
        Interaction? interaction = VendorGoogleInteractionsJson.DeserializeInteraction(json.ToString(Formatting.None));
        List<InteractionContent> audio = interaction?.Steps?.Where(x => x.Type == "model_output")
            .SelectMany(x => x.Content ?? []).Where(x => x.Type == "audio" && x.Data is not null).ToList() ?? [];
        if (audio.Count == 0) throw new InvalidDataException("Gemini returned no inline speech audio.");

        MemoryStream output = new();
        try
        {
            foreach (InteractionContent part in audio)
            {
                byte[] bytes = Convert.FromBase64String(part.Data!);
                await output.WriteAsync(bytes, 0, bytes.Length, request.CancellationToken).ConfigureAwait(false);
            }
            output.Position = 0;
            ApiResultBase headers = new();
            provider.ParseInboundHeaders(headers, transport.Response);
            SpeechTtsResult result = new(new StreamResponse { Stream = output, Response = transport.Response, Headers = headers })
            {
                GoogleInteraction = interaction, MimeType = audio[0].MimeType,
                SampleRate = audio[0].SampleRate, Channels = audio[0].Channels
            };
            // The caller owns this response and the decoded audio. The JSON transport stream is disposed here.
            transport.Response = null;
            return result;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    internal static async IAsyncEnumerable<SpeechTtsChunk> Stream(SpeechRequest request, TornadoApi api,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, request.CancellationToken);
        InteractionCreateRequest body = CreateRequest(request, true);
        string? mimeType = body.ResponseFormat?.MimeType;
        int? sampleRate = body.ResponseFormat?.SampleRate ?? 24000;
        int? channels = 1;
        await foreach (InteractionStreamEvent evt in api.Interactions.CreateStream(body, cancellationToken: linked.Token).ConfigureAwait(false))
        {
            if (evt.NormalizedEventType == InteractionStreamEventTypes.Error)
                throw new InvalidDataException("Gemini speech stream failed: " + evt.Data);
            if (evt.Step?.Content?.FirstOrDefault(x => x.Type == "audio") is { } start)
            {
                mimeType = start.MimeType ?? mimeType;
                sampleRate = start.SampleRate ?? sampleRate;
                channels = start.Channels ?? channels;
            }
            if (evt.Delta?["type"]?.ToString() != "audio") continue;
            mimeType = evt.Delta["mime_type"]?.ToString() ?? mimeType;
            sampleRate = evt.Delta["sample_rate"]?.Value<int?>() ?? sampleRate;
            channels = evt.Delta["channels"]?.Value<int?>() ?? channels;
            if (evt.Delta["data"]?.ToString() is not { Length: > 0 } data) continue;
            yield return new SpeechTtsChunk
            {
                Data = Convert.FromBase64String(data), MimeType = mimeType, SampleRate = sampleRate, Channels = channels
            };
        }
    }
}
