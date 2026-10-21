using System.Collections.Generic;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Code;
using LlmTornado.Images.Models.Google;
using Newtonsoft.Json;

namespace LlmTornado.Images.Vendors.Google;

/// <summary>
/// generateContent request for native Gemini image generation and editing.
/// </summary>
internal class VendorGoogleGeminiImageRequest
{
    [JsonProperty("contents")]
    public List<VendorGoogleChatRequest.VendorGoogleChatRequestMessage> Contents { get; set; }

    [JsonProperty("generationConfig")]
    public VendorGoogleChatRequestGenerationConfig GenerationConfig { get; set; }

    public VendorGoogleGeminiImageRequest(ImageGenerationRequest request, IEndpointProvider provider)
    {
        ImageGenerationVideoContext? video = request.VideoContext;
        string? modelName = request.Model?.Name;
        if (!ImageModelGoogleGemini.IsNativeImageModel(modelName))
        {
            throw new System.ArgumentException($"Model '{modelName}' is not a native Gemini image model.");
        }

        if (video is not null && !ImageModelGoogleGemini.SupportsVideoToImage(modelName))
        {
            throw new System.ArgumentException($"Model '{modelName}' does not support video-to-image generation.");
        }
        request.OverrideUrl($"{provider.ApiUrl(CapabilityEndpoints.ImageGeneration, null)}/{modelName}:generateContent");

        Contents =
        [
            new VendorGoogleChatRequest.VendorGoogleChatRequestMessage
            {
                Role = "user",
                Parts =
                [
                    new VendorGoogleChatRequestMessagePart
                    {
                        Text = request.Prompt
                    }
                ]
            }
        ];

        if (video is not null)
        {
            Contents[0].Parts.Insert(0, new VendorGoogleChatRequestMessagePart
            {
                FileData = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartFileData
                {
                    FileUri = video.FileUri,
                    MimeType = video.MimeType
                },
                VideoMetadata = video.Metadata is null ? null : new VendorGoogleChatRequest.VendorGoogleChatRequestMetadataVideo
                {
                    Fps = video.Metadata.Fps,
                    StartOffset = video.Metadata.StartOffset,
                    EndOffset = video.Metadata.EndOffset
                }
            });
        }

        GenerationConfig = new VendorGoogleChatRequestGenerationConfig
        {
            ResponseModalities = ["TEXT", "IMAGE"],
            ThinkingConfig = request.VendorExtensions?.Google?.ThinkingLevel is string thinkingLevel
                ? new VendorGoogleChatRequestThinkingConfig { ThinkingLevel = thinkingLevel } : null
        };

        if (request.VendorExtensions?.Google?.ImageSize is not null)
        {
            GenerationConfig.ImageConfig = new VendorGoogleChatRequestImageConfig
            {
                ImageSize = request.VendorExtensions.Google.ImageSize switch
                {
                    ImageGenerationRequestGoogleExtensionsImageSizes.Resolution512 => "512",
                    ImageGenerationRequestGoogleExtensionsImageSizes.Resolution1K => "1K",
                    ImageGenerationRequestGoogleExtensionsImageSizes.Resolution2K => "2K",
                    ImageGenerationRequestGoogleExtensionsImageSizes.Resolution4K => "4K",
                    _ => null
                }
            };
        }

        if (request.Size is not null || request.VendorExtensions?.Google?.AspectRatio is not null)
        {
            GenerationConfig.ImageConfig ??= new VendorGoogleChatRequestImageConfig();
            GenerationConfig.ImageConfig.AspectRatio = request.VendorExtensions?.Google?.AspectRatio switch
            {
                ImageGenerationRequestGoogleExtensionsAspectRatios.Square => "1:1",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Portrait2x3 => "2:3",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Landscape3x2 => "3:2",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Portrait3x4 => "3:4",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Landscape4x3 => "4:3",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Portrait4x5 => "4:5",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Landscape5x4 => "5:4",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Portrait9x16 => "9:16",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Landscape16x9 => "16:9",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Ultrawide21x9 => "21:9",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Portrait1x4 => "1:4",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Landscape4x1 => "4:1",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Portrait1x8 => "1:8",
                ImageGenerationRequestGoogleExtensionsAspectRatios.Landscape8x1 => "8:1",
                _ => GenerationConfig.ImageConfig.AspectRatio
            } ?? request.Size switch
            {
                TornadoImageSizes.Size256x256 or TornadoImageSizes.Size512x512 or TornadoImageSizes.Size1024x1024 => "1:1",
                TornadoImageSizes.Size896x1280 => "3:4",
                TornadoImageSizes.Size1280x896 => "4:3",
                TornadoImageSizes.Size1408x768 => "16:9",
                TornadoImageSizes.Size768x1408 => "9:16",
                _ => null
            };
        }
    }
    public VendorGoogleGeminiImageRequest(ImageEditRequest request, IEndpointProvider provider)
        : this(new ImageGenerationRequest(request.Prompt)
        {
            Model = request.Model,
            Size = request.Size,
            VendorExtensions = new ImageGenerationRequestVendorExtensions { Google = request.VendorExtensions?.Google }
        }, provider)
    {
        List<TornadoInputFile> images = request.Images?.Count > 0 ? request.Images
            : request.Image is not null ? [request.Image] : [];
        if (images.Count == 0) throw new System.ArgumentException("At least one source image is required for native Gemini editing.");
        foreach (TornadoInputFile image in images)
        {
            if (image.Base64 is null) throw new System.ArgumentException("Gemini image editing requires image data.");
            Contents[0].Parts.Insert(Contents[0].Parts.Count - 1, new VendorGoogleChatRequestMessagePart
            {
                InlineData = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartInlineData
                {
                    Data = image.Base64.StripDataUriPrefix(),
                    MimeType = image.MimeType ?? "image/png"
                }
            });
        }
    }

}
