using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// Claude 5 class models from Anthropic (Fable 5/5.1, Mythos 5/5.1, Opus 5, Sonnet 5).
/// </summary>
public class ChatModelAnthropicClaude5 : IVendorModelClassProvider
{
    /// <summary>
    /// Claude Fable 5 — Anthropic's most capable widely released model at launch, built for the most demanding
    /// reasoning and long-horizon agentic work. 1M context window by default, 128K max output,
    /// adaptive thinking always on (no extended thinking, no <c>thinking.type = "disabled"</c>),
    /// effort parameter supported (defaults to <c>high</c>), high-res vision (2576px long edge).
    /// Raw chain of thought is never returned; use <c>thinking.display</c> to request summaries.
    /// API model ID: <c>claude-fable-5</c>.
    /// </summary>
    public static readonly ChatModel ModelFable = new ChatModel(Fable5ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelFable"/>
    /// </summary>
    public readonly ChatModel Fable = ModelFable;

    /// <summary>
    /// Claude Fable 5.1 — successor to Fable 5 for long-running agentic coding, knowledge work, and research.
    /// 1M context, 128K max output, always-on adaptive thinking. Cache reads are 0.025x input price.
    /// <c>tool_choice</c> types <c>any</c> and <c>tool</c> are rejected; use <c>auto</c>/<c>none</c> or strict tools.
    /// API model ID: <c>claude-fable-5-1</c>.
    /// </summary>
    public static readonly ChatModel ModelFable51 = new ChatModel(Fable51ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelFable51"/>
    /// </summary>
    public readonly ChatModel Fable51 = ModelFable51;

    /// <summary>
    /// Claude Mythos 5 — Fable 5 equivalent for Project Glasswing participants (invitation-only).
    /// Same 1M context, 128K max output, and always-on adaptive thinking as Fable 5.
    /// API model ID: <c>claude-mythos-5</c>.
    /// </summary>
    public static readonly ChatModel ModelMythos = new ChatModel(Mythos5ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelMythos"/>
    /// </summary>
    public readonly ChatModel Mythos = ModelMythos;

    /// <summary>
    /// Claude Mythos 5.1 — Fable 5.1 equivalent for Project Glasswing participants (invitation-only).
    /// Same capabilities and <c>tool_choice</c> restrictions as Fable 5.1.
    /// API model ID: <c>claude-mythos-5-1</c>.
    /// </summary>
    public static readonly ChatModel ModelMythos51 = new ChatModel(Mythos51ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelMythos51"/>
    /// </summary>
    public readonly ChatModel Mythos51 = ModelMythos51;

    /// <summary>
    /// Claude Opus 5 — step-change over Opus 4.8 for deep reasoning, agentic coding, and long-horizon work.
    /// 1M context (default and maximum), 128K max output, thinking on by default, full effort ladder including <c>max</c>.
    /// <c>thinking.type = "disabled"</c> is accepted only at effort <c>high</c> or below.
    /// API model ID: <c>claude-opus-5</c>.
    /// </summary>
    public static readonly ChatModel ModelOpus = new ChatModel(Opus5ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelOpus"/>
    /// </summary>
    public readonly ChatModel Opus = ModelOpus;

    /// <summary>
    /// Claude Sonnet 5 — the best combination of speed and intelligence.
    /// 1M context window by default, 128K max output, adaptive thinking on by default,
    /// effort parameter supported (defaults to <c>high</c> on the Claude API and Claude Code),
    /// high-res vision (2576px long edge). Manual extended thinking is rejected.
    /// API model ID: <c>claude-sonnet-5</c>.
    /// </summary>
    public static readonly ChatModel ModelSonnet = new ChatModel(Sonnet5ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelSonnet"/>
    /// </summary>
    public readonly ChatModel Sonnet = ModelSonnet;

    /// <summary>
    /// Claude Opus 5.5. 1M context window, 128K maximum output tokens.
    /// Always-on adaptive thinking; use effort to control depth. Forced tool choice is unsupported.
    /// </summary>
    public static readonly ChatModel ModelOpus55 = new ChatModel(ChatModelAnthropicHelper.Opus55ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelOpus55"/>
    /// </summary>
    public readonly ChatModel Opus55 = ModelOpus55;

    /// <summary>
    /// Claude Sonnet 5.5. 1M context window, 128K maximum output tokens.
    /// Adaptive thinking by default; between_tools suppresses up-front thinking. Forced tool choice is unsupported.
    /// </summary>
    public static readonly ChatModel ModelSonnet55 = new ChatModel(ChatModelAnthropicHelper.Sonnet55ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelSonnet55"/>
    /// </summary>
    public readonly ChatModel Sonnet55 = ModelSonnet55;

    /// <summary>
    /// Claude Haiku 5.5. 1M context window, 128K maximum output tokens.
    /// Adaptive thinking by default; thinking can be disabled at high effort or below.
    /// </summary>
    public static readonly ChatModel ModelHaiku55 = new ChatModel(ChatModelAnthropicHelper.Haiku55ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelHaiku55"/>
    /// </summary>
    public readonly ChatModel Haiku55 = ModelHaiku55;

    /// <summary>
    /// All known Claude 5 models from Anthropic.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelFable, ModelFable51, ModelMythos, ModelMythos51, ModelOpus, ModelSonnet, ModelOpus55, ModelSonnet55, ModelHaiku55
    ]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelAnthropicClaude5()
    {

    }

    private const string Fable5ModelId = ChatModelAnthropicHelper.Fable5ModelId;
    private const string Fable51ModelId = ChatModelAnthropicHelper.Fable51ModelId;
    private const string Mythos5ModelId = ChatModelAnthropicHelper.Mythos5ModelId;
    private const string Mythos51ModelId = ChatModelAnthropicHelper.Mythos51ModelId;
    private const string Opus5ModelId = ChatModelAnthropicHelper.Opus5ModelId;
    private const string Sonnet5ModelId = ChatModelAnthropicHelper.Sonnet5ModelId;
    private const int Opus47PlusContextTokens = ChatModelAnthropicHelper.Opus47PlusContextTokens;
}
