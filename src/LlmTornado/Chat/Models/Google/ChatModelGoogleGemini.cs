using System;
using System.Collections.Generic;
using System.Diagnostics;
using LlmTornado.Code;
using LlmTornado.Code.Models;
using LlmTornado.Models.Vendors.Google;

namespace LlmTornado.Chat.Models;

/// <summary>
/// Gemini class models from Google.
/// </summary>
public class ChatModelGoogleGemini : IVendorModelClassProvider
{
    /// <summary>
    /// Gemini 2.5 Pro is our state-of-the-art thinking model, capable of reasoning over complex problems in code, math, and STEM, as well as analyzing large datasets, codebases, and documents using long context.
    /// </summary>
    public static readonly ChatModel ModelGemini25Pro = new ChatModel("gemini-2.5-pro", LLmProviders.Google, 1_000_000)
    {
        ReasoningTokensMin = 128,
        ReasoningTokensMax = 32_768,
        ReasoningTokensSpecialValues = [ -1 ]
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelGemini25Pro"/>
    /// </summary>
    public readonly ChatModel Gemini25Pro = ModelGemini25Pro;
    
    /// <summary>
    /// Alias pointing to gemini-3.1-pro-preview (previously gemini-3-pro-preview). Best for complex tasks that require broad world knowledge and advanced reasoning across modalities.
    /// </summary>
    public static readonly ChatModel ModelGeminiProLatest = new ChatModel("gemini-pro-latest", LLmProviders.Google, 1_000_000) 
    {
        ReasoningTokensMin = 128,
        ReasoningTokensMax = 32_768,
        ReasoningTokensSpecialValues = [ -1 ]
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelGeminiProLatest"/>
    /// </summary>
    public readonly ChatModel GeminiProLatest = ModelGeminiProLatest;
    
    /// <summary>
    /// Alias pointing to gemini-3.8-flash. Gemini 3.8 Flash is our most intelligent Flash model for long-horizon software engineering, autonomous agents, and enterprise workflows.
    /// </summary>
    public static readonly ChatModel ModelGeminiFlashLatest = new ChatModel("gemini-flash-latest", LLmProviders.Google, 1_048_576) 
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 65_536,
        ReasoningTokensSpecialValues = [ -1 ]
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelGeminiFlashLatest"/>
    /// </summary>
    public readonly ChatModel GeminiFlashLatest = ModelGeminiFlashLatest;
    
    /// <summary>
    /// Our best model in terms of price-performance, offering well-rounded capabilities. 2.5 Flash is best for large scale processing, low-latency, high volume tasks that require thinking, and agentic use cases.
    /// </summary>
    public static readonly ChatModel ModelGemini25Flash = new ChatModel("gemini-2.5-flash", LLmProviders.Google, 1_000_000) 
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 24_576,
        ReasoningTokensSpecialValues = [ -1 ]
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelGemini25Flash"/>
    /// </summary>
    public readonly ChatModel Gemini25Flash = ModelGemini25Flash;
    
    /// <summary>
    /// Alias pointing to gemini-3.5-flash-lite. Fastest, most cost-effective 3.5 Flash-Lite model for high-throughput execution.
    /// </summary>
    public static readonly ChatModel ModelGeminiFlashLiteLatest = new ChatModel("gemini-flash-lite-latest", LLmProviders.Google, 1_048_576) 
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 65_536,
        ReasoningTokensSpecialValues = [ -1 ]
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelGeminiFlashLiteLatest"/>
    /// </summary>
    public readonly ChatModel GeminiFlashLiteLatest = ModelGeminiFlashLiteLatest;
    
    /// <summary>
    /// A Gemini 2.5 Flash model optimized for cost-efficiency and high throughput.
    /// </summary>
    public static readonly ChatModel ModelGemini25FlashLite = new ChatModel("gemini-2.5-flash-lite", LLmProviders.Google, 1_000_000) 
    {
        ReasoningTokensMin = 512,
        ReasoningTokensMax = 24_576,
        ReasoningTokensSpecialValues = [ 0, -1 ]
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelGemini25FlashLite"/>
    /// </summary>
    public readonly ChatModel Gemini25FlashLite = ModelGemini25FlashLite;

    /// <summary>
    /// Gemini 3.8 Flash is our most intelligent Flash model, engineered for long-horizon software engineering, autonomous agents, and complex enterprise workflows.
    /// Input: Text, Image, Video, Audio, and PDF. Output: Text. Context: 1M in / 64k out. Thinking: low/medium (default)/high. <c>minimal</c> is not supported.
    /// Computer Use (Preview) is supported. Default model behind <c>gemini-flash-latest</c>.
    /// </summary>
    public static readonly ChatModel ModelGemini38Flash = new ChatModel("gemini-3.8-flash", LLmProviders.Google, 1_048_576)
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 65_536,
        ReasoningTokensSpecialValues = [ -1 ],
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini38Flash"/>
    /// </summary>
    public readonly ChatModel Gemini38Flash = ModelGemini38Flash;

    /// <summary>
    /// Gemini 3.7 Flash is the previous-generation Flash model for complex coding, agentic workflows, and reliable multi-step execution.
    /// Input: Text, Image, Video, Audio, and PDF. Output: Text. Context: 1M in / 64k out. Thinking: low/medium (default)/high. <c>minimal</c> is not supported.
    /// Computer Use (Preview) and agentic video understanding are supported.
    /// </summary>
    public static readonly ChatModel ModelGemini37Flash = new ChatModel("gemini-3.7-flash", LLmProviders.Google, 1_048_576)
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 65_536,
        ReasoningTokensSpecialValues = [ -1 ],
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Deprecated, ReplacementModel = "gemini-3.8-flash", Message = "Requests automatically route to the replacement as of October 8, 2026." }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini37Flash"/>
    /// </summary>
    public readonly ChatModel Gemini37Flash = ModelGemini37Flash;

    /// <summary>
    /// Gemini 3.6 Flash balances speed and multimodal capabilities across general agentic and everyday tasks, with improved token efficiency versus 3.5 Flash.
    /// Input: Text, Image, Video, Audio, and PDF. Output: Text. Context: 1M in / 64k out. Thinking: low/medium (default)/high. <c>minimal</c> is not supported.
    /// Computer Use (Preview) and agentic video understanding are supported.
    /// </summary>
    public static readonly ChatModel ModelGemini36Flash = new ChatModel("gemini-3.6-flash", LLmProviders.Google, 1_048_576)
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 65_536,
        ReasoningTokensSpecialValues = [ -1 ],
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini36Flash"/>
    /// </summary>
    public readonly ChatModel Gemini36Flash = ModelGemini36Flash;

    /// <summary>
    /// Gemini 3.5 Flash is a legacy Flash model providing baseline speed and foundational performance for routine, high-throughput workloads.
    /// Input: Text, Image, Video, Audio, and PDF. Output: Text. Context: 1M in / 64k out. Default thinking level: medium.
    /// Computer Use (Preview) is supported (browser, mobile, desktop).
    /// </summary>
    public static readonly ChatModel ModelGemini35Flash = new ChatModel("gemini-3.5-flash", LLmProviders.Google, 1_048_576)
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 65_536,
        ReasoningTokensSpecialValues = [ -1 ],
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Deprecated, ReplacementModel = "gemini-3.6-flash", Message = "Requests automatically route to the replacement as of October 8, 2026." }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini35Flash"/>
    /// </summary>
    public readonly ChatModel Gemini35Flash = ModelGemini35Flash;

    /// <summary>
    /// Gemini 3.5 Flash-Lite is the fastest, most cost-effective 3.5 model for high-throughput execution and subagent tasks.
    /// Input: Text, Image, Video, Audio, and PDF. Output: Text. Context: 1M in / 64k out.
    /// Thinking: minimal (default)/low/medium/high. Computer Use (Preview) and agentic video understanding are supported.
    /// Default model behind <c>gemini-flash-lite-latest</c>.
    /// </summary>
    public static readonly ChatModel ModelGemini35FlashLite = new ChatModel("gemini-3.5-flash-lite", LLmProviders.Google, 1_048_576)
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 65_536,
        ReasoningTokensSpecialValues = [ -1 ],
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini35FlashLite"/>
    /// </summary>
    public readonly ChatModel Gemini35FlashLite = ModelGemini35FlashLite;

    /// <summary>
    /// Gemini 3.1 Flash-Lite is a low-latency, cost-efficient multimodal model for high-volume agentic workflows and lightweight tasks.
    /// Input: Text, Image, Video, Audio, and PDF. Output: Text. Context: 1M in / 64k out.
    /// </summary>
    public static readonly ChatModel ModelGemini31FlashLite = new ChatModel("gemini-3.1-flash-lite", LLmProviders.Google, 1_048_576)
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 24_576,
        ReasoningTokensSpecialValues = [ -1 ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini31FlashLite"/>
    /// </summary>
    public readonly ChatModel Gemini31FlashLite = ModelGemini31FlashLite;

    /// <summary>
    /// Fast and versatile performance across a diverse variety of tasks (stable).
    /// </summary>
    [Obsolete("Shut down June 1, 2026. Use ModelGemini25Flash or ModelGemini35Flash instead.")]
    public static readonly ChatModel ModelGemini2Flash001 = new ChatModel("gemini-2.0-flash-001", LLmProviders.Google, 1_000_000)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo
        {
            Stage = GoogleModelStage.Legacy,
            RetirementTime = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ReplacementModel = "gemini-2.5-flash"
        }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini2Flash001"/>
    /// </summary>
    [Obsolete("Shut down June 1, 2026. Use ModelGemini25Flash or ModelGemini35Flash instead.")]
    public readonly ChatModel Gemini2Flash001 = ModelGemini2Flash001;

    /// <summary>
    /// Fast and versatile performance across a diverse variety of tasks (latest).
    /// </summary>
    [Obsolete("Shut down June 1, 2026. Use ModelGemini25Flash or ModelGemini35Flash instead.")]
    public static readonly ChatModel ModelGemini2FlashLatest = new ChatModel("gemini-2.0-flash", LLmProviders.Google, 1_000_000)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo
        {
            Stage = GoogleModelStage.Legacy,
            RetirementTime = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ReplacementModel = "gemini-2.5-flash"
        }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini2FlashLatest"/>
    [Obsolete("Shut down June 1, 2026. Use ModelGemini25Flash or ModelGemini35Flash instead.")]
    public readonly ChatModel Gemini2FlashLatest = ModelGemini2FlashLatest;
    
    /// <summary>
    /// A Gemini 2.0 Flash model optimized for cost efficiency and low latency (stable).
    /// </summary>
    [Obsolete("Shut down June 1, 2026. Use ModelGemini25FlashLite or ModelGemini31FlashLite instead.")]
    public static readonly ChatModel ModelGemini2FlashLite001 = new ChatModel("gemini-2.0-flash-lite-001", LLmProviders.Google, 1_000_000)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo
        {
            Stage = GoogleModelStage.Legacy,
            RetirementTime = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ReplacementModel = "gemini-2.5-flash-lite"
        }
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelGemini2FlashLite001"/>
    /// </summary>
    [Obsolete("Shut down June 1, 2026. Use ModelGemini25FlashLite or ModelGemini31FlashLite instead.")]
    public readonly ChatModel Gemini2FlashLite001 = ModelGemini2FlashLite001;
    
    /// <summary>
    /// A Gemini 2.0 Flash model optimized for cost efficiency and low latency (latest).
    /// </summary>
    [Obsolete("Shut down June 1, 2026. Use ModelGemini25FlashLite or ModelGemini31FlashLite instead.")]
    public static readonly ChatModel ModelGemini2FlashLiteLatest = new ChatModel("gemini-2.0-flash-lite", LLmProviders.Google, 1_000_000)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo
        {
            Stage = GoogleModelStage.Legacy,
            RetirementTime = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ReplacementModel = "gemini-2.5-flash-lite"
        }
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelGemini2FlashLiteLatest"/>
    /// </summary>
    [Obsolete("Shut down June 1, 2026. Use ModelGemini25FlashLite or ModelGemini31FlashLite instead.")]
    public readonly ChatModel Gemini2FlashLiteLatest = ModelGemini2FlashLiteLatest;
    
    /// <summary>
    /// Complex reasoning tasks such as code and text generation, text editing, problem-solving, data extraction and generation.
    /// </summary>
    [Obsolete("Use ModelGemini25Pro instead.")]
    public static readonly ChatModel ModelGemini15ProLatest = new ChatModel("gemini-1.5-pro-latest", LLmProviders.Google, 1_000_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelGemini15ProLatest"/>
    /// </summary>
    [Obsolete("Use ModelGemini25Pro instead.")]
    public readonly ChatModel Gemini15ProLatest = ModelGemini15ProLatest;

    /// <summary>
    /// Complex reasoning tasks such as code and text generation, text editing, problem-solving, data extraction and generation.
    /// </summary>
    [Obsolete("Use ModelGemini25Pro instead.")]
    public static readonly ChatModel ModelGemini15Pro = new ChatModel("gemini-1.5-pro", LLmProviders.Google, 1_000_000);

    /// <summary>
    /// <inheritdoc cref="ModelGemini15Pro"/>
    /// </summary>
    [Obsolete("Use ModelGemini25Pro instead.")]
    public readonly ChatModel Gemini15Pro = ModelGemini15Pro;

    /// <summary>
    /// Complex reasoning tasks such as code and text generation, text editing, problem-solving, data extraction and generation.
    /// </summary>
    [Obsolete("Use ModelGemini25Pro instead.")]
    public static readonly ChatModel ModelGemini15Pro001 = new ChatModel("gemini-1.5-pro-001", LLmProviders.Google, 1_000_000);

    /// <summary>
    /// <inheritdoc cref="ModelGemini15Pro001"/>
    /// </summary>
    [Obsolete("Use ModelGemini25Pro instead.")]
    public readonly ChatModel Gemini15Pro001 = ModelGemini15Pro001;
    
    /// <summary>
    /// Complex reasoning tasks such as code and text generation, text editing, problem-solving, data extraction and generation.
    /// </summary>
    [Obsolete("Use ModelGemini25Pro instead.")]
    public static readonly ChatModel ModelGemini15Pro002 = new ChatModel("gemini-1.5-pro-002", LLmProviders.Google, 1_000_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelGemini15Pro002"/>
    /// </summary>
    [Obsolete("Use ModelGemini25Pro instead.")]
    public readonly ChatModel Gemini15Pro002 = ModelGemini15Pro002;

    /// <summary>
    /// Gemini 1.5 Flash-8B is a small model designed for lower intelligence tasks.
    /// </summary>
    [Obsolete("Use ModelGeminiFlashLatest instead.")]
    public static readonly ChatModel ModelGemini15Flash8BLatest = new ChatModel("gemini-1.5-flash-8b-latest", LLmProviders.Google, 1_000_000);

    /// <summary>
    /// <inheritdoc cref="ModelGemini15Flash8BLatest"/>
    /// </summary>
    [Obsolete("Use ModelGeminiFlashLatest instead.")]
    public readonly ChatModel Gemini15Flash8BLatest = ModelGemini15Flash8BLatest;

    /// <summary>
    /// Gemini 1.5 Flash-8B is a small model designed for lower intelligence tasks.
    /// </summary>
    [Obsolete("Use ModelGeminiFlashLatest instead.")]
    public static readonly ChatModel ModelGemini15Flash8B = new ChatModel("gemini-1.5-flash-8b", LLmProviders.Google, 1_000_000);

    /// <summary>
    /// <inheritdoc cref="ModelGemini15Flash8B"/>
    /// </summary>
    [Obsolete("Use ModelGeminiFlashLatest instead.")]
    public readonly ChatModel Gemini15Flash8B = ModelGemini15Flash8B;
    
    /// <summary>
    /// Gemini 2.5 Flash Image is our latest, fastest, and most efficient natively multimodal model that lets you generate and edit images conversationally.
    /// </summary>
    public static readonly ChatModel ModelGemini25FlashImage = new ChatModel("gemini-2.5-flash-image", LLmProviders.Google, 32_768);
    
    /// <summary>
    /// <inheritdoc cref="ModelGemini25FlashImage"/>
    /// </summary>
    public readonly ChatModel Gemini25FlashImage = ModelGemini25FlashImage;

    /// <summary>
    /// Nano Banana Pro (Gemini 3 Pro Image) is a reasoning-driven image generation and editing model for professional asset production.
    /// Supports high-resolution output (1K, 2K, 4K), advanced text rendering, Google Search grounding, and thinking mode.
    /// Input: Image and Text. Output: Image and Text. Context: 65k in / 32k out.
    /// </summary>
    public static readonly ChatModel ModelGemini3ProImage = new ChatModel("gemini-3-pro-image", LLmProviders.Google, 65_536);

    /// <summary>
    /// <inheritdoc cref="ModelGemini3ProImage"/>
    /// </summary>
    public readonly ChatModel Gemini3ProImage = ModelGemini3ProImage;

    /// <summary>
    /// Nano Banana 2 (Gemini 3.1 Flash Image) delivers high-quality image generation and conversational editing at Flash speed.
    /// Supports resolutions 512, 1K, 2K, and 4K; image search grounding; and extended aspect ratios (including 1:4, 4:1, 1:8, 8:1).
    /// Input: Text and Image / PDF. Output: Image and Text. Context: 131k in / 32k out.
    /// </summary>
    public static readonly ChatModel ModelGemini31FlashImage = new ChatModel("gemini-3.1-flash-image", LLmProviders.Google, 131_072)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Deprecated, ReplacementModel = "gemini-nano-banana-2.1" }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini31FlashImage"/>
    /// </summary>
    public readonly ChatModel Gemini31FlashImage = ModelGemini31FlashImage;

    /// <summary>
    /// Nano Banana 2 Lite (Gemini 3.1 Flash Lite Image) is the efficiency specialist for ultra-low latency, cost-effective image generation and editing.
    /// Input: Text and Image. Output: Image and Text.
    /// </summary>
    public static readonly ChatModel ModelGemini31FlashLiteImage = new ChatModel("gemini-3.1-flash-lite-image", LLmProviders.Google, 131_072)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini31FlashLiteImage"/>
    /// </summary>
    public readonly ChatModel Gemini31FlashLiteImage = ModelGemini31FlashLiteImage;

    /// <summary>
    /// Gemini 3.8 Live is the default Live API model for low-latency voice agents and real-time dialogue without reasoning delays.
    /// Features interleaved reasoning, default asynchronous function calling, visual context, and 97+ languages. Live API only.
    /// </summary>
    public static readonly ChatModel ModelGemini38Live = new ChatModel("gemini-3.8-live", LLmProviders.Google, 131_072)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini38Live"/>
    /// </summary>
    public readonly ChatModel Gemini38Live = ModelGemini38Live;

    /// <summary>
    /// Gemini 3.8 Live Extended Thinking is a high-reasoning Live API audio-to-audio model for background reasoning during live interactions.
    /// Supports configurable thinking while continuing to stream audio. Live API only.
    /// </summary>
    public static readonly ChatModel ModelGemini38LiveExtendedThinking = new ChatModel("gemini-3.8-live-extended-thinking", LLmProviders.Google, 131_072)
    {
        ReasoningTokensMin = 0,
        ReasoningTokensMax = 65_536,
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini38LiveExtendedThinking"/>
    /// </summary>
    public readonly ChatModel Gemini38LiveExtendedThinking = ModelGemini38LiveExtendedThinking;

    /// <summary>
    /// Gemini 3.5 Transcribe is a dedicated speech-to-text model with utterance-based language detection (85+ languages),
    /// speaker diarization, word-level timestamps, Smart transcription, and custom vocabulary biasing (up to 1,000 terms).
    /// Unary generateContent: audio up to 1 hour (30 minutes with diarization or timestamps).
    /// </summary>
    public static readonly ChatModel ModelGemini35Transcribe = new ChatModel("gemini-3.5-transcribe", LLmProviders.Google, 131_072)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary>
    /// <inheritdoc cref="ModelGemini35Transcribe"/>
    /// </summary>
    public readonly ChatModel Gemini35Transcribe = ModelGemini35Transcribe;
    
    /// <summary>Flagship reasoning model. Unified input/output context; low, medium (default), or high thinking. Implicit caching only.</summary>
    public static readonly ChatModel ModelGemini4Argon = new ChatModel("gemini-4-argon", LLmProviders.Google, 1_048_576)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary><inheritdoc cref="ModelGemini4Argon"/></summary>
    public readonly ChatModel Gemini4Argon = ModelGemini4Argon;

    /// <summary>Native image generation and editing with text, image, video, and PDF input. Thinking levels: minimal, medium (default), high.</summary>
    public static readonly ChatModel ModelGeminiNanoBanana21 = new ChatModel("gemini-nano-banana-2.1", LLmProviders.Google, 131_072)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary><inheritdoc cref="ModelGeminiNanoBanana21"/></summary>
    public readonly ChatModel GeminiNanoBanana21 = ModelGeminiNanoBanana21;

    /// <summary>Expressive text-to-speech with 16,384 maximum output tokens. Supports streaming and custom voices.</summary>
    public static readonly ChatModel ModelGemini38FlashTts = new ChatModel("gemini-3.8-flash-tts", LLmProviders.Google, 8_192)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary><inheritdoc cref="ModelGemini38FlashTts"/></summary>
    public readonly ChatModel Gemini38FlashTts = ModelGemini38FlashTts;

    /// <summary>Low-latency text-to-speech with 16,384 maximum output tokens. Supports streaming and custom voices.</summary>
    public static readonly ChatModel ModelGemini38FlashLiteTts = new ChatModel("gemini-3.8-flash-lite-tts", LLmProviders.Google, 8_192)
    {
        GoogleLifecycle = new GoogleModelLifecycleInfo { Stage = GoogleModelStage.Stable }
    };

    /// <summary><inheritdoc cref="ModelGemini38FlashLiteTts"/></summary>
    public readonly ChatModel Gemini38FlashLiteTts = ModelGemini38FlashLiteTts;

    /// <summary>
    /// All known Gemini models from Google.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelGemini15ProLatest, 
        ModelGemini15Pro, ModelGemini15Pro001, ModelGemini15Pro002, ModelGemini15Flash8B, ModelGemini15Flash8BLatest, ModelGemini2Flash001,
        ModelGemini2FlashLatest, ModelGemini2FlashLite001, ModelGemini2FlashLiteLatest, ModelGemini25Pro, ModelGemini25Flash, 
        ModelGemini25FlashLite, ModelGemini31FlashLite, ModelGemini35FlashLite, ModelGemini35Flash, ModelGemini36Flash, ModelGemini37Flash, ModelGemini38Flash,
        ModelGeminiFlashLiteLatest, ModelGeminiFlashLatest, ModelGeminiProLatest, ModelGemini25FlashImage,
        ModelGemini3ProImage, ModelGemini31FlashImage, ModelGemini31FlashLiteImage,
        ModelGemini38Live, ModelGemini38LiveExtendedThinking, ModelGemini35Transcribe,
        ModelGemini4Argon, ModelGeminiNanoBanana21, ModelGemini38FlashTts, ModelGemini38FlashLiteTts
    ]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelGoogleGemini()
    {

    }
}