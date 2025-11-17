using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// GPT-5.1 class models from OpenAI.
/// </summary>
public class ChatModelOpenAiGpt51 : IVendorModelClassProvider
{
    /// <summary>
    /// Latest snapshot of GPT-5.1, the best model for coding and agentic tasks with configurable reasoning effort. Currently gpt-5.1-2025-11-13.
    /// </summary>
    public static readonly ChatModel ModelV51 = new ChatModel("gpt-5.1", LLmProviders.OpenAi, 400_000, [ "gpt-5.1-2025-11-13" ])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Chat, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV51"/>
    /// </summary>
    public readonly ChatModel V51 = ModelV51;
    
    /// <summary>
    /// Latest snapshot of GPT-5.1 chat model, alias for gpt-5.1.
    /// </summary>
    public static readonly ChatModel ModelV51ChatLatest = new ChatModel("gpt-5.1-chat-latest", LLmProviders.OpenAi, 400_000, [])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Chat, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV51ChatLatest"/>
    /// </summary>
    public readonly ChatModel V51ChatLatest = ModelV51ChatLatest;
    
    /// <summary>
    /// GPT-5.1 Codex is optimized for long-running, agentic coding tasks in Codex or Codex-like harnesses.
    /// </summary>
    public static readonly ChatModel ModelV51Codex = new ChatModel("gpt-5.1-codex", LLmProviders.OpenAi, 400_000, [])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV51Codex"/>
    /// </summary>
    public readonly ChatModel V51Codex = ModelV51Codex;
    
    /// <summary>
    /// GPT-5.1 Codex Mini is a smaller variant optimized for agentic coding tasks.
    /// </summary>
    public static readonly ChatModel ModelV51CodexMini = new ChatModel("gpt-5.1-codex-mini", LLmProviders.OpenAi, 400_000, [])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV51CodexMini"/>
    /// </summary>
    public readonly ChatModel V51CodexMini = ModelV51CodexMini;

    /// <summary>
    /// All known GPT-5.1 models from OpenAI.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelV51, ModelV51ChatLatest, ModelV51Codex, ModelV51CodexMini
    ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;
    
    internal ChatModelOpenAiGpt51()
    {
        
    }
}

