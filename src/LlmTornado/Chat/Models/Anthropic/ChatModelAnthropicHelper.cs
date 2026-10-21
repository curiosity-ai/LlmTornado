using System;
using System.Collections.Generic;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// Shared capability checks for Anthropic Claude models.
/// </summary>
public static class ChatModelAnthropicHelper
{
    /// <summary>
    /// Default context window on Claude Opus 4.7+ (1M tokens, no beta header).
    /// </summary>
    public const int Opus47PlusContextTokens = 1_000_000;

    /// <summary>
    /// Maximum output tokens on Claude Opus 4.7+.
    /// </summary>
    public const int Opus47PlusMaxOutputTokens = 128_000;

    /// <summary>
    /// High-resolution vision long-edge limit in pixels on Claude Opus 4.7+.
    /// </summary>
    public const int Opus47PlusHighResVisionLongEdgePx = 2576;

    /// <summary>
    /// API model ID for Claude Opus 4.8 (internal codename: NextOpus).
    /// </summary>
    public const string Opus48ModelId = "claude-opus-4-8";

    /// <summary>
    /// API model ID for Claude Opus 4.7.
    /// </summary>
    public const string Opus47ModelId = "claude-opus-4-7";

    /// <summary>
    /// API model ID for Claude Opus 5.
    /// </summary>
    public const string Opus5ModelId = "claude-opus-5";

    /// <summary>
    /// API model ID for Claude Fable 5.
    /// </summary>
    public const string Fable5ModelId = "claude-fable-5";

    /// <summary>
    /// API model ID for Claude Fable 5.1.
    /// </summary>
    public const string Fable51ModelId = "claude-fable-5-1";

    /// <summary>
    /// API model ID for Claude Mythos 5 (Project Glasswing).
    /// </summary>
    public const string Mythos5ModelId = "claude-mythos-5";

    /// <summary>
    /// API model ID for Claude Mythos 5.1 (Project Glasswing).
    /// </summary>
    public const string Mythos51ModelId = "claude-mythos-5-1";

    /// <summary>
    /// API model ID for Claude Sonnet 5.
    /// </summary>
    public const string Sonnet5ModelId = "claude-sonnet-5";

    /// <summary>API model ID for Claude Opus 5.5.</summary>
    public const string Opus55ModelId = "claude-opus-5-5";

    /// <summary>API model ID for Claude Sonnet 5.5.</summary>
    public const string Sonnet55ModelId = "claude-sonnet-5-5";

    /// <summary>API model ID for Claude Haiku 5.5.</summary>
    public const string Haiku55ModelId = "claude-haiku-5-5";

    /// <summary>
    /// Returns true for <c>claude-opus-4-7</c>, <c>claude-opus-4-8</c>, and Claude 5 generation models.
    /// </summary>
    public static bool IsOpus47OrNewer(string? modelName)
    {
        if (modelName is null)
        {
            return false;
        }

        return modelName.StartsWith(Opus47ModelId, StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith(Opus48ModelId, StringComparison.OrdinalIgnoreCase)
            || IsClaude5Model(modelName);
    }

    /// <summary>
    /// Returns true for Claude 5 generation models (Fable 5/5.1, Mythos 5/5.1, Opus 5, Sonnet 5).
    /// </summary>
    public static bool IsClaude5Model(string? modelName)
    {
        if (modelName is null)
        {
            return false;
        }

        return modelName.StartsWith(Haiku55ModelId, StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith(Fable5ModelId, StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith(Sonnet5ModelId, StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith(Opus5ModelId, StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith(Mythos5ModelId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true for Claude Opus 5 and later Opus 5.x snapshots.
    /// </summary>
    public static bool IsOpus5OrNewer(string? modelName)
    {
        return modelName is not null
            && modelName.StartsWith(Opus5ModelId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true for Claude Fable 5.1 / Mythos 5.1 (and later 5.1.x snapshots).
    /// </summary>
    public static bool IsFable51OrNewer(string? modelName)
    {
        if (modelName is null)
        {
            return false;
        }

        return modelName.StartsWith(Fable51ModelId, StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith(Mythos51ModelId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fable 5 / Mythos 5 families reject <c>thinking.type = "disabled"</c> (adaptive thinking is always on).
    /// </summary>
    public static bool IsAlwaysOnAdaptiveThinkingModel(string? modelName)
    {
        if (modelName is null)
        {
            return false;
        }

        return modelName.StartsWith(Opus55ModelId, StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith(Fable5ModelId, StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith(Mythos5ModelId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Models that turn adaptive thinking on when the <c>thinking</c> field is omitted (Claude 5 generation).
    /// Send <c>thinking: { type: "disabled" }</c> to turn it off on models that allow it.
    /// </summary>
    public static bool IsThinkingOnByDefault(string? modelName) => IsClaude5Model(modelName);

    /// <summary>
    /// Claude Fable 5.1 and Mythos 5.1 reject <c>tool_choice</c> types <c>any</c> and <c>tool</c>.
    /// </summary>
    public static bool RejectsForcedToolChoice(string? modelName) => IsFable51OrNewer(modelName)
        || modelName?.StartsWith(Opus55ModelId, StringComparison.OrdinalIgnoreCase) == true
        || SupportsBetweenToolsThinking(modelName);

    /// <summary>
    /// On Claude Opus 5+, <c>thinking.type = "disabled"</c> is rejected when effort is <c>xhigh</c> or <c>max</c>.
    /// </summary>
    public static bool RestrictsDisabledThinkingToHighEffortOrBelow(string? modelName) => IsOpus5OrNewer(modelName)
        || modelName?.StartsWith(Haiku55ModelId, StringComparison.OrdinalIgnoreCase) == true
        || SupportsBetweenToolsThinking(modelName);

    /// <summary>Sonnet 5.5 uses between-tools thinking instead of disabled thinking.</summary>
    public static bool SupportsBetweenToolsThinking(string? modelName)
        => modelName?.StartsWith(Sonnet55ModelId, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Returns true for models that only support adaptive thinking (Opus 4.7+ and Claude 5 generation).
    /// These models reject manual extended thinking and require adaptive thinking when thinking is enabled.
    /// </summary>
    public static bool IsAdaptiveOnlyThinkingModel(string? modelName)
        => IsOpus47OrNewer(modelName) || IsClaude5Model(modelName);

    /// <summary>
    /// Returns true for Claude Opus 4.6 and newer Opus generations.
    /// </summary>
    public static bool IsOpus46OrNewer(string? modelName)
    {
        if (modelName is null)
        {
            return false;
        }

        return modelName.StartsWith("claude-opus-4-6", StringComparison.OrdinalIgnoreCase)
            || IsOpus47OrNewer(modelName);
    }

    /// <summary>
    /// Returns true when <c>thinking.type = "adaptive"</c> is supported (Opus 4.6+, Sonnet 4.6, Opus 4.7/4.8, Claude 5).
    /// On Opus 4.7+ and Claude 5 manual <c>thinking.type = "enabled"</c> is upgraded to adaptive automatically.
    /// On Claude Fable 5 / Mythos 5 adaptive thinking is always on and <c>thinking.type = "disabled"</c> is not supported.
    /// </summary>
    public static bool SupportsAdaptiveThinking(string? modelName)
    {
        if (modelName is null)
        {
            return false;
        }

        return modelName.StartsWith("claude-opus-4-6", StringComparison.OrdinalIgnoreCase)
            || modelName.StartsWith("claude-sonnet-4-6", StringComparison.OrdinalIgnoreCase)
            || IsOpus47OrNewer(modelName)
            || IsClaude5Model(modelName);
    }

    /// <summary>
    /// Returns true when the effort parameter is supported (serialized to <c>output_config.effort</c>).
    /// Claude 5 models default to <c>high</c> effort on the Claude API and Claude Code.
    /// </summary>
    public static bool IsEffortCompatibleModel(string? modelName)
    {
        if (modelName is null)
        {
            return false;
        }

        return modelName.StartsWith("claude-opus-4-5", StringComparison.OrdinalIgnoreCase)
            || IsOpus46OrNewer(modelName)
            || modelName.StartsWith("claude-sonnet-4-6", StringComparison.OrdinalIgnoreCase)
            || IsClaude5Model(modelName);
    }

    /// <summary>
    /// Models that reject non-default <c>temperature</c>, <c>top_p</c>, and <c>top_k</c> with HTTP 400.
    /// Applies to Claude Opus 4.7+ and Claude 5 generation (adaptive-only thinking models).
    /// </summary>
    public static bool RejectsNonDefaultSamplingParams(string? modelName) => IsAdaptiveOnlyThinkingModel(modelName);

    /// <summary>
    /// Models that reject manual extended thinking (<c>thinking.type = enabled</c> with <c>budget_tokens</c>).
    /// Applies to Claude Opus 4.7+ and Claude 5 generation (adaptive-only thinking models).
    /// </summary>
    public static bool RejectsManualExtendedThinking(string? modelName) => IsAdaptiveOnlyThinkingModel(modelName);

    /// <summary>
    /// Models that only support adaptive thinking when thinking is enabled.
    /// Applies to Claude Opus 4.7+ and Claude 5 generation.
    /// </summary>
    public static bool RequiresAdaptiveThinkingWhenEnabled(string? modelName) => IsAdaptiveOnlyThinkingModel(modelName);

    /// <summary>
    /// High-resolution vision (up to 2576px long edge) is automatic on Claude Opus 4.7+ and Claude 5.
    /// </summary>
    public static bool SupportsHighResVision(string? modelName) => IsAdaptiveOnlyThinkingModel(modelName);

    /// <summary>
    /// Known adaptive-only thinking chat models (Opus 4.7+ and Claude 5) for policy checks keyed by <see cref="IModel.Name"/>.
    /// </summary>
    internal static HashSet<IModel> Opus47PlusModels => LazyOpus47PlusModels.Value;

    private static readonly Lazy<HashSet<IModel>> LazyOpus47PlusModels = new Lazy<HashSet<IModel>>(() =>
    [
        ChatModelAnthropicClaude47.ModelOpus,
        ChatModelAnthropicClaude48.ModelOpus,
        ChatModelAnthropicClaude48.ModelNextOpus,
        ChatModelAnthropicClaude5.ModelFable,
        ChatModelAnthropicClaude5.ModelFable51,
        ChatModelAnthropicClaude5.ModelMythos,
        ChatModelAnthropicClaude5.ModelMythos51,
        ChatModelAnthropicClaude5.ModelOpus,
        ChatModelAnthropicClaude5.ModelSonnet,
        ChatModelAnthropicClaude5.ModelOpus55,
        ChatModelAnthropicClaude5.ModelSonnet55,
        ChatModelAnthropicClaude5.ModelHaiku55
    ]);

    /// <summary>
    /// Strips sampling parameters that cause HTTP 400 on Claude Opus 4.7+.
    /// </summary>
    public static void ClearSamplingParamsIfUnsupported(ChatRequest request)
    {
        if (request.Model is null || !RejectsNonDefaultSamplingParams(request.Model.Name))
        {
            return;
        }

        request.Temperature = null;
        request.TopP = null;
    }
}
