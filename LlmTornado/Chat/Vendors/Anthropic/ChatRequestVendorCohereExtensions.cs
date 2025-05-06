using System;
using System.Collections.Generic;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;

namespace LlmTornado.Chat.Vendors.Anthropic;

/// <summary>
/// Cache settings used by Anthropic.
/// </summary>
public class AnthropicCacheSettings
{
    /// <summary>
    /// "ephemeral" type of cache, shared object.
    /// </summary>
    public static readonly AnthropicCacheSettings Ephemeral = new AnthropicCacheSettings();

    [JsonProperty("type")]
    public string Type { get; set; } = "ephemeral";

    private AnthropicCacheSettings()
    {

    }
}
/// <summary>
/// Thinking settings for Claude 3.7+ models.
/// </summary>
public class AnthropicThinkingSettings
{
    /// <summary>
    /// The budget_tokens parameter determines the maximum number of tokens Claude is allowed use for its internal reasoning process. Larger budgets can improve response quality by enabling more thorough analysis for complex problems, although Claude may not use the entire budget allocated, especially at ranges above 32K.
    /// <br/><b>Note: budget_tokens must always be less than the max_tokens specified.</b>
    /// </summary>
    public int? BudgetTokens { get; set; }

    /// <summary>
    /// Whether thinking is enabled
    /// </summary>
    public bool Enabled { get; set; }
}
/// <summary>
/// Anthropic chat request item.
/// </summary>
public interface IAnthropicChatRequestItem
{

}
/// <summary>
///     Chat features supported only by Anthropic.
/// </summary>
public class ChatRequestVendorAnthropicExtensions
{
    /// <summary>
    ///     Enables modification of the outbound chat request just before sending it. Use this to control cache in chat-like scenarios.<br/>
    ///     Arguments: <b>System message</b>; <b>User, Assistant messages</b>; <b>Tools</b>
    /// </summary>
    public Action<VendorAnthropicChatRequestMessageContent?, List<VendorAnthropicChatRequestMessageContent>, List<VendorAnthropicToolFunction>?>? OutboundRequest;

    /// <summary>
    /// Thinking settings for Claude 3.7+ models.
    /// Instead of using this vendor-specific setting, <see cref="ChatRequest.ReasoningBudget"/> can be used.
    /// </summary>
    public AnthropicThinkingSettings? Thinking { get; set; }
}