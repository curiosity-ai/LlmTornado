using Newtonsoft.Json;

namespace LlmTornado.Chat.Vendors.Anthropic;

/// <summary>Requests a signed summary instead of a normal reply (compact-2026-09-04 beta).</summary>
public class AnthropicOnDemandCompaction
{
    [JsonProperty("type")]
    public string Type => "summarize";
}
