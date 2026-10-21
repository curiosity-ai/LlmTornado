using LlmTornado.Images.Vendors.XAi;

namespace LlmTornado.Images;

/// <summary>
///     Image edit request features supported only by a single/few providers with no shared equivalent.
/// </summary>
public class ImageEditRequestVendorExtensions
{
    /// <summary>Native Gemini image output resolution and aspect ratio.</summary>
    public Vendors.Google.ImageGenerationRequestGoogleExtensions? Google { get; set; }

    /// <summary>
    ///     xAI extensions.
    /// </summary>
    public ImageEditRequestXAiExtensions? XAi { get; set; }

    /// <summary>
    ///     Empty extensions.
    /// </summary>
    public ImageEditRequestVendorExtensions()
    {
        
    }
    
    /// <summary>
    ///     xAI extensions.
    /// </summary>
    /// <param name="xAiExtensions"></param>
    public ImageEditRequestVendorExtensions(ImageEditRequestXAiExtensions xAiExtensions)
    {
        XAi = xAiExtensions;
    }
}
