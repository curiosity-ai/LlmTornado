using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Audio.Models.Google;

/// <summary>
/// Google Gemini speech models.
/// </summary>
public class AudioModelGoogleGemini : IVendorModelClassProvider
{
    /// <summary>Gemini 3.8 Flash TTS, with styled and multi-speaker speech and custom voices.</summary>
    public static readonly AudioModel ModelGemini38FlashTts = new AudioModel("gemini-3.8-flash-tts", LLmProviders.Google);

    /// <inheritdoc cref="ModelGemini38FlashTts"/>
    public readonly AudioModel Gemini38FlashTts = ModelGemini38FlashTts;

    /// <summary>Gemini 3.8 Flash-Lite TTS, optimized for low-latency speech synthesis.</summary>
    public static readonly AudioModel ModelGemini38FlashLiteTts = new AudioModel("gemini-3.8-flash-lite-tts", LLmProviders.Google);

    /// <inheritdoc cref="ModelGemini38FlashLiteTts"/>
    public readonly AudioModel Gemini38FlashLiteTts = ModelGemini38FlashLiteTts;

    /// <summary>
    /// gemini-3.5-transcribe - High-accuracy unary speech-to-text with language detection (85+ languages),
    /// speaker diarization, word-level timestamps, Smart transcription, and custom vocabulary biasing.
    /// Audio up to 1 hour (30 minutes with diarization or timestamps).
    /// </summary>
    public static readonly AudioModel ModelGemini35Transcribe = new AudioModel("gemini-3.5-transcribe", LLmProviders.Google);

    /// <summary>
    /// <inheritdoc cref="ModelGemini35Transcribe"/>
    /// </summary>
    public readonly AudioModel Gemini35Transcribe = ModelGemini35Transcribe;

    /// <summary>
    /// gemini-3.5-transcribe-live - Low-latency bidirectional streaming speech-to-text over the Live API.
    /// Supports interim and finalized events, Smart transcription, and multiple VAD strategies. Sessions up to 10 minutes.
    /// </summary>
    public static readonly AudioModel ModelGemini35TranscribeLive = new AudioModel("gemini-3.5-transcribe-live", LLmProviders.Google);

    /// <summary>
    /// <inheritdoc cref="ModelGemini35TranscribeLive"/>
    /// </summary>
    public readonly AudioModel Gemini35TranscribeLive = ModelGemini35TranscribeLive;

    /// <summary>
    /// All known Gemini speech models from Google.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelGemini35Transcribe,
        ModelGemini35TranscribeLive,
        ModelGemini38FlashTts,
        ModelGemini38FlashLiteTts
    ]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal AudioModelGoogleGemini()
    {

    }
}
