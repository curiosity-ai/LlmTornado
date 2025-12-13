using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// GPT-5.2 class models from OpenAI.
/// </summary>
public class ChatModelOpenAiGpt52 : IVendorModelClassProvider
{
    /// <summary>
    /// GPT-5.2 is the best general-purpose model, part of the GPT-5 flagship model family. 
    /// Best for complex reasoning, broad world knowledge, and code-heavy or multi-step agentic tasks.
    /// </summary>
    public static readonly ChatModel ModelV52 = new ChatModel("gpt-5.2", LLmProviders.OpenAi, 400_000, [])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Chat, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV52"/>
    /// </summary>
    public readonly ChatModel V52 = ModelV52;
    
    /// <summary>
    /// GPT-5.2 Pro uses more compute to think harder and provide consistently better answers. 
    /// Best for tough problems that may take longer to solve but require harder thinking.
    /// </summary>
    public static readonly ChatModel ModelV52Pro = new ChatModel("gpt-5.2-pro", LLmProviders.OpenAi, 400_000, [])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV52Pro"/>
    /// </summary>
    public readonly ChatModel V52Pro = ModelV52Pro;
    
    /// <summary>
    /// Latest snapshot of GPT-5.2 chat model. The model powering ChatGPT.
    /// </summary>
    public static readonly ChatModel ModelV52ChatLatest = new ChatModel("gpt-5.2-chat-latest", LLmProviders.OpenAi, 400_000, [])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Chat, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV52ChatLatest"/>
    /// </summary>
    public readonly ChatModel V52ChatLatest = ModelV52ChatLatest;

    /// <summary>
    /// All known GPT-5.2 models from OpenAI.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelV52, ModelV52Pro, ModelV52ChatLatest
    ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;
    
    internal ChatModelOpenAiGpt52()
    {
        
    }
}
