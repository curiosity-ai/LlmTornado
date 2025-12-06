using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models.XAi;

/// <summary>
/// Grok 4.1 class models from xAI.
/// </summary>
public class ChatModelXAiGrok41 : IVendorModelClassProvider
{
    /// <summary>
    /// A frontier multimodal model optimized specifically for high-performance agentic tool calling.
    /// </summary>
    public static readonly ChatModel ModelV41FastReasoning = new ChatModel("grok-4-1-fast-reasoning", LLmProviders.XAi, 2_000_000, [ "grok-4-1-fast", "grok-4-1-fast-reasoning-latest" ]);

    /// <summary>
    /// <inheritdoc cref="ModelV41FastReasoning"/>
    /// </summary>
    public readonly ChatModel V41FastReasoning = ModelV41FastReasoning;
    
    /// <summary>
    /// A frontier multimodal model optimized specifically for high-performance agentic tool calling (non-reasoning variant).
    /// </summary>
    public static readonly ChatModel ModelV41FastNonReasoning = new ChatModel("grok-4-1-fast-non-reasoning", LLmProviders.XAi, 2_000_000, [ "grok-4-1-fast-non-reasoning-latest" ]);

    /// <summary>
    /// <inheritdoc cref="ModelV41FastNonReasoning"/>
    /// </summary>
    public readonly ChatModel V41FastNonReasoning = ModelV41FastNonReasoning;
    
    /// <summary>
    /// All Grok 4.1 models.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelV41FastReasoning, ModelV41FastNonReasoning
    ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;
    
    internal ChatModelXAiGrok41()
    {
        
    }
}

