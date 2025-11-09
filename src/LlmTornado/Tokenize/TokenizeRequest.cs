using System;
using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Tokenize.Vendors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tokenize;

/// <summary>
///     A request to the Tokenize API for counting tokens in text or messages.
/// </summary>
public class TokenizeRequest : ISerializableRequest
{
    /// <summary>
    ///     Creates a new, empty <see cref="TokenizeRequest" />
    /// </summary>
    public TokenizeRequest()
    {
    }

    /// <summary>
    ///     Creates a new tokenize request with text.
    /// </summary>
    /// <param name="model">The model to use for tokenization</param>
    /// <param name="text">The text to tokenize</param>
    public TokenizeRequest(ChatModel model, string text)
    {
        Model = model;
        Text = text;
    }

    /// <summary>
    ///     Creates a new tokenize request with messages.
    /// </summary>
    /// <param name="model">The model to use for tokenization</param>
    /// <param name="messages">The messages to tokenize</param>
    public TokenizeRequest(ChatModel model, List<ChatMessage> messages)
    {
        Model = model;
        Messages = messages;
    }

    /// <summary>
    ///     Creates a new tokenize request with messages and tools.
    /// </summary>
    /// <param name="model">The model to use for tokenization</param>
    /// <param name="messages">The messages to tokenize</param>
    /// <param name="tools">The tools to include in tokenization (for Anthropic)</param>
    public TokenizeRequest(ChatModel model, List<ChatMessage> messages, List<Tool> tools)
    {
        Model = model;
        Messages = messages;
        Tools = tools;
    }

    /// <summary>
    ///     The model to use for tokenization
    /// </summary>
    [JsonProperty("model")]
    [JsonConverter(typeof(Chat.Models.ChatModelJsonConverter))]
    public ChatModel? Model { get; set; }

    /// <summary>
    ///     The text to tokenize (for simple text tokenization, e.g., Cohere)
    /// </summary>
    [JsonProperty("text")]
    public string? Text { get; set; }

    /// <summary>
    ///     The messages to tokenize (for chat-based tokenization)
    /// </summary>
    [JsonProperty("messages")]
    public List<ChatMessage>? Messages { get; set; }

    /// <summary>
    ///     The tools to include in tokenization (for Anthropic)
    /// </summary>
    [JsonProperty("tools")]
    public List<Tool>? Tools { get; set; }

    [JsonIgnore]
    internal string? UrlOverride { get; set; }

    internal void OverrideUrl(string url)
    {
        UrlOverride = url;
    }

    /// <summary>
    ///     Serializes the tokenize request into the request body, based on the conventions used by the LLM provider.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public TornadoRequestContent Serialize(IEndpointProvider provider)
    {
        string content = provider.Provider switch
        {
            LLmProviders.MoonshotAi => PreparePayload(new VendorMoonshotAiTokenizeRequest(this, provider), this, provider, EndpointBase.NullSettings),
            LLmProviders.Anthropic => PreparePayload(new VendorAnthropicTokenizeRequest(this, provider), this, provider, EndpointBase.NullSettings),
            LLmProviders.Google => PreparePayload(new VendorGoogleTokenizeRequest(this, provider), this, provider, EndpointBase.NullSettings),
            LLmProviders.Cohere => PreparePayload(new VendorCohereTokenizeRequest(this, provider), this, provider, EndpointBase.NullSettings),
            _ => string.Empty
        };

        return new TornadoRequestContent(content, Model, UrlOverride, provider, CapabilityEndpoints.Tokenize);
    }

    /// <summary>
    ///     Serializes the tokenize request into the request body, based on the conventions used by the LLM provider.
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public TornadoRequestContent Serialize(IEndpointProvider provider, RequestSerializeOptions options)
    {
        return Serialize(provider);
    }

    private static string PreparePayload(object sourceObject, TokenizeRequest context, IEndpointProvider provider, JsonSerializerSettings? settings)
    {
        return sourceObject.SerializeRequestObject(context, provider, RequestActionTypes.Unknown, settings);
    }
}

