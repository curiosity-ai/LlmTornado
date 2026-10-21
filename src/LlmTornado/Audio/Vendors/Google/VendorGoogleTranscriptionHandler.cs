using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Interactions;

namespace LlmTornado.Audio.Vendors.Google;

internal static class VendorGoogleTranscriptionHandler
{
    internal static async Task<InteractionCreateRequest> CreateRequest(TranscriptionRequest request)
    {
        if (request.Model.Name != "gemini-3.5-transcribe")
            throw new ArgumentException("Unary Gemini transcription requires gemini-3.5-transcribe. Use Live for live transcription.", nameof(request));
        if (request.ResponseFormat is AudioTranscriptionResponseFormats.Srt or AudioTranscriptionResponseFormats.Vtt)
            throw new NotSupportedException("Gemini returns transcript text and word annotations, not SRT or VTT.");
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("Gemini transcription uses custom vocabulary rather than a prompt.", nameof(request));
        if (request.TimestampGranularities?.Contains(TimestampGranularities.Segment) == true)
            throw new ArgumentException("Gemini transcription supports word timestamps only.", nameof(request));

        InteractionTranscriptionConfig? supplied = request.Google?.Configuration;
        InteractionTranscriptionMode? suppliedMode = supplied?.Mode;
        InteractionTranscriptionConfig config = new()
        {
            LanguageCodes = supplied?.LanguageCodes is { } languages ? [..languages] : string.IsNullOrWhiteSpace(request.Language) ? null : [request.Language],
            CustomVocabulary = supplied?.CustomVocabulary is { } vocabulary ? [..vocabulary] : request.ContextBias is { } bias ? [..bias] : null,
            Mode = suppliedMode is null ? InteractionTranscriptionMode.Verbatim() : new InteractionTranscriptionMode
            {
                Type = suppliedMode.Type, DiarizationMode = suppliedMode.DiarizationMode,
                TimestampGranularities = suppliedMode.TimestampGranularities is { } timestamps ? [..timestamps] : null
            }
        };
        if (request.Diarize == true || request.ResponseFormat == AudioTranscriptionResponseFormats.DiarizedJson)
            config.Mode.DiarizationMode ??= "speaker";
        if (request.TimestampGranularities?.Contains(TimestampGranularities.Word) == true)
            config.Mode.TimestampGranularities ??= ["word"];
        config.Validate();
        request.CancellationToken.ThrowIfCancellationRequested();

        InteractionContent audio = new() { Type = "audio", MimeType = request.Google?.MimeType };
        if (request.File?.Data is { } data)
        {
            audio.Data = Convert.ToBase64String(data);
            audio.MimeType ??= FileMimeType(request.File);
        }
        else if (request.File?.File is { } stream)
        {
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer, 81920, request.CancellationToken).ConfigureAwait(false);
            audio.Data = Convert.ToBase64String(buffer.ToArray());
            audio.MimeType ??= FileMimeType(request.File);
        }
        else if (!string.IsNullOrWhiteSpace(request.Url))
        {
            if (request.Url!.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = request.Url.IndexOf(',');
                int marker = request.Url.IndexOf(";base64", StringComparison.OrdinalIgnoreCase);
                if (comma < 0 || marker < 5 || marker + 7 != comma)
                    throw new ArgumentException("Inline Gemini audio must be a base64 data URL.", nameof(request));
                audio.Data = request.Url.Substring(comma + 1);
                audio.MimeType ??= request.Url.Substring(5, marker - 5);
            }
            else audio.Uri = request.Url;
        }
        else throw new ArgumentException("An audio file or uploaded audio URI is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(audio.MimeType))
            throw new ArgumentException("Set Google.MimeType when transcribing a file URI.", nameof(request));
        return new InteractionCreateRequest
        {
            Model = request.Model.Name, Input = InteractionInput.FromContents(audio), Store = false,
            GenerationConfig = new() { TranscriptionConfig = config }
        };
    }

    private static string FileMimeType(AudioFile file) => file.ContentType is AudioFileTypes.M4a or AudioFileTypes.Mp4 ? "audio/m4a" : file.GetContentType;

    internal static async Task<TranscriptionResult?> CreateTranscription(TranscriptionRequest request, IEndpointProvider provider, EndpointBase endpoint)
    {
        InteractionCreateRequest body = await CreateRequest(request).ConfigureAwait(false);
        Interaction? result = await endpoint.HttpPost1<Interaction>(provider, CapabilityEndpoints.Interactions,
            provider.ApiUrl(CapabilityEndpoints.Interactions, null), body.Serialize(), request.Model, body, request.CancellationToken).ConfigureAwait(false);
        return result is null ? null : ToResult(result);
    }

    internal static TranscriptionResult ToResult(Interaction interaction)
    {
        List<InteractionContent> content = interaction.Steps?.Where(x => x.Type == "model_output")
            .SelectMany(x => x.Content ?? []).Where(x => x.Type == "text").ToList() ?? [];
        TranscriptionResult result = new()
        {
            Text = string.Concat(content.Select(x => x.Text)), Task = "transcription", GoogleInteraction = interaction,
            Model = interaction.Model,
            Usage = interaction.Usage is null ? null : new TranscriptionUsage
            {
                Type = "tokens", InputTokens = interaction.Usage.TotalInputTokens,
                OutputTokens = interaction.Usage.TotalOutputTokens, TotalTokens = interaction.Usage.TotalTokens
            }
        };
        foreach (InteractionAnnotation word in content.SelectMany(x => x.Annotations ?? []).Where(x => x.Type == "word_info"))
        {
            result.Words.Add(new TranscriptionWord
            {
                Word = word.Text ?? string.Empty, Speaker = word.Speaker,
                StartOffset = word.StartOffset, EndOffset = word.EndOffset,
                Start = Seconds(word.StartOffset), End = Seconds(word.EndOffset)
            });
        }
        return result;
    }

    private static float Seconds(string? offset) => offset is not null && offset.EndsWith("s", StringComparison.Ordinal)
        && float.TryParse(offset.Substring(0, offset.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) ? seconds : 0;
}
