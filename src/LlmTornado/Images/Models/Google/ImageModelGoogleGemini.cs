using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Images.Models.Google;

/// <summary>
/// Gemini native image models from Google (generateContent-based image generation).
/// </summary>
public class ImageModelGoogleGemini : IVendorModelClassProvider
{
    /// <summary>Nano Banana 2.1: native image generation, editing, and video-to-image.</summary>
    public static readonly ImageModel ModelGeminiNanoBanana21 = new ImageModel("gemini-nano-banana-2.1", LLmProviders.Google);

    /// <summary><inheritdoc cref="ModelGeminiNanoBanana21"/></summary>
    public readonly ImageModel GeminiNanoBanana21 = ModelGeminiNanoBanana21;

    /// <summary>
    /// Nano Banana 2 (Gemini 3.1 Flash Image). Supports video-to-image, text-to-image, and conversational editing.
    /// </summary>
    public static readonly ImageModel ModelGemini31FlashImage = new ImageModel("gemini-3.1-flash-image", LLmProviders.Google);

    /// <summary>
    /// <inheritdoc cref="ModelGemini31FlashImage"/>
    /// </summary>
    public readonly ImageModel Gemini31FlashImage = ModelGemini31FlashImage;

    /// <summary>
    /// Preview variant of Gemini 3.1 Flash Image.
    /// </summary>
    [Obsolete("Shut down June 25, 2026. Use ModelGemini31FlashImage instead.")]
    public static readonly ImageModel ModelGemini31FlashImagePreview = new ImageModel("gemini-3.1-flash-image-preview", LLmProviders.Google);

    /// <summary>
    /// <inheritdoc cref="ModelGemini31FlashImagePreview"/>
    /// </summary>
    [Obsolete("Shut down June 25, 2026. Use Gemini31FlashImage instead.")]
    public readonly ImageModel Gemini31FlashImagePreview = ModelGemini31FlashImagePreview;

    /// <summary>
    /// Nano Banana 2 Lite (Gemini 3.1 Flash Lite Image). Ultra-low latency, cost-effective image generation and editing.
    /// </summary>
    public static readonly ImageModel ModelGemini31FlashLiteImage = new ImageModel("gemini-3.1-flash-lite-image", LLmProviders.Google);

    /// <summary>
    /// <inheritdoc cref="ModelGemini31FlashLiteImage"/>
    /// </summary>
    public readonly ImageModel Gemini31FlashLiteImage = ModelGemini31FlashLiteImage;

    /// <summary>Whether an image model uses Gemini generateContent rather than Imagen predict.</summary>
    public static bool IsNativeImageModel(string? modelName)
        => modelName is not null && (modelName == "gemini-nano-banana-2.1"
            || modelName.StartsWith("gemini-", StringComparison.Ordinal) && modelName.Contains("image"));

    /// <summary>
    /// Models that support video-to-image generation.
    /// </summary>
    public static HashSet<string> VideoToImageModels { get; } =
    [
        ModelGeminiNanoBanana21.Name,
        ModelGemini31FlashImage.Name,
        ModelGemini31FlashImagePreview.Name
    ];

    /// <summary>
    /// Returns whether the model supports video-to-image generation.
    /// </summary>
    public static bool SupportsVideoToImage(string? modelName)
    {
        return modelName is not null && VideoToImageModels.Contains(modelName);
    }

    /// <summary>
    /// All known Gemini image models from Google.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() =>
    [
        ModelGeminiNanoBanana21,
        ModelGemini31FlashImage,
        ModelGemini31FlashLiteImage,
        ModelGemini31FlashImagePreview
    ]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ImageModelGoogleGemini()
    {
    }
}
