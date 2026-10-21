using System.Collections.Generic;

namespace LlmTornado.Chat.Vendors.Anthropic;

/// <summary>
/// Chat response features supported only by Anthropic.
/// </summary>
public class ChatResponseVendorAnthropicExtensions
{
    /// <summary>Thinking blocks dropped or allowed despite a binding mismatch. Null when diagnostics were not returned.</summary>
    public List<AnthropicInputTransformation>? InputTransformations { get; set; }

    /// <summary>
    /// Cache diagnostics returned when cache diagnostics was enabled on the request.
    /// A null value on the response means either the first turn (nothing to compare) or no divergence was found.
    /// </summary>
    public AnthropicCacheDiagnosticsResponse? CacheDiagnostics { get; set; }
    
    /// <summary>
    /// Empty extensions.
    /// </summary>
    public ChatResponseVendorAnthropicExtensions()
    {
        
    }
}
