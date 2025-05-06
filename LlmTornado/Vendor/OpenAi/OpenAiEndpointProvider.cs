using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code.Sse;
using LlmTornado.Threads;
using Newtonsoft.Json;
using ToolCall = LlmTornado.ChatFunctions.ToolCall;

namespace LlmTornado.Code.Vendor;

/// <summary>
/// 
/// </summary>
internal class OpenAiEndpointProvider : BaseEndpointProvider, IEndpointProvider, IEndpointProviderExtended
{
    private const           string          DataString        = "data:";
    private const           string          DoneString        = "[DONE]";
    private static readonly HashSet<string> toolFinishReasons = ["function_call", "tool_calls"];

    public static   Version                                     OutboundVersion   { get; set; } = HttpVersion.Version20;
    public override HashSet<string>                             ToolFinishReasons => toolFinishReasons;
    public          Func<CapabilityEndpoints, string?, string>? UrlResolver       { get; set; }

    public OpenAiEndpointProvider(TornadoApi api) : base(api)
    {
        Provider = LLmProviders.OpenAi;
        StoreApiAuth();
    }

    public OpenAiEndpointProvider(TornadoApi api, LLmProviders provider) : base(api)
    {
        Provider = provider;
        StoreApiAuth();
    }

    enum ChatStreamParsingStates
    {
        Text,
        Tools
    }

    public static string GetEndpointUrlFragment(CapabilityEndpoints endpoint, LLmProviders provider = LLmProviders.OpenAi)
    {
        return endpoint switch
        {
            CapabilityEndpoints.Audio           => "audio",
            CapabilityEndpoints.Chat            => "chat/completions",
            CapabilityEndpoints.Completions     => "completions",
            CapabilityEndpoints.Embeddings      => "embeddings",
            CapabilityEndpoints.FineTuning      => "fine_tuning",
            CapabilityEndpoints.Files           => "files",
            CapabilityEndpoints.ImageGeneration => "images/generations",
            CapabilityEndpoints.ImageEdit       => "images/edits",
            CapabilityEndpoints.Models          => "models",
            CapabilityEndpoints.Moderation      => "moderations",
            CapabilityEndpoints.Assistants      => "assistants",
            CapabilityEndpoints.Threads         => "threads",
            CapabilityEndpoints.VectorStores    => "vector_stores",
            _                                   => throw new Exception($"OpenAI doesn't support endpoint {endpoint}")
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="url"></param>
    /// <returns></returns>
    public override string ApiUrl(CapabilityEndpoints endpoint, string? url)
    {
        return UrlResolver is not null ? UrlResolver.Invoke(endpoint, url) : $"{string.Format(Api.ApiUrlFormat ?? "https://api.openai.com/{0}/{1}", Api.ApiVersion, GetEndpointUrlFragment(endpoint))}{url}";
    }

    public override HttpRequestMessage OutboundMessage(string url, HttpMethod verb, object? data, bool streaming)
    {
        HttpRequestMessage req = new HttpRequestMessage(verb, url)
        {
            Version = OutboundVersion
        };
        req.Headers.Add("User-Agent",  EndpointBase.GetUserAgent().Trim());
        req.Headers.Add("OpenAI-Beta", "assistants=v2");

        ProviderAuthentication? auth = Api.GetProvider(Provider).Auth;

        if (auth is not null)
        {
            if (auth.ApiKey is not null)
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.ApiKey.Trim());
                req.Headers.Add("api-key", auth.ApiKey.Trim());
            }

            if (auth.Organization is not null)
            {
                req.Headers.Add("OpenAI-Organization", auth.Organization.Trim());
            }
        }

        return req;
    }

    public override void ParseInboundHeaders(object? res, HttpResponseMessage response)
    {

    }

    public override void ParseInboundHeaders<T>(T res, HttpResponseMessage response)
    {
        res.Provider = this;

        if (response.Headers.TryGetValues("Openai-Organization", out IEnumerable<string>? orgH))
        {
            res.Organization = orgH.FirstOrDefault();
        }

        if (response.Headers.TryGetValues("X-Request-ID", out IEnumerable<string>? reqId))
        {
            res.RequestId = reqId.FirstOrDefault();
        }

        if (response.Headers.TryGetValues("Openai-Processing-Ms", out IEnumerable<string>? pms))
        {
            string? processing = pms.FirstOrDefault();

            if (processing is not null && int.TryParse(processing, out int n))
            {
                res.ProcessingTime = TimeSpan.FromMilliseconds(n);
            }
        }

        if (response.Headers.TryGetValues("Openai-Version", out IEnumerable<string>? oav))
        {
            res.RequestId = oav.FirstOrDefault();
        }
    }

    public override T? InboundMessage<T>(string jsonData, string? postData) where T : default
    {
        return JsonConvert.DeserializeObject<T>(jsonData);
    }

    public override object? InboundMessage(Type type, string jsonData, string? postData)
    {
        return JsonConvert.DeserializeObject(jsonData, type);
    }

    public override async IAsyncEnumerable<ChatResult?> InboundStream(StreamReader reader, ChatRequest request)
    {
        ChatStreamParsingStates  state            = ChatStreamParsingStates.Text;
        bool                     parseTools       = request.Tools?.Count > 0;
        ChatResult?              toolsAccumulator = null;
        ChatMessage?             toolsMessage     = null;
        StringBuilder?           plaintextBuilder = null;
        StringBuilder?           reasoningBuilder = null;
        ChatUsage?               usage            = null;
        ChatMessageFinishReasons finishReason     = ChatMessageFinishReasons.Unknown;

#if DEBUG
        List<string> data = [];
#endif

        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync(request.CancellationToken))
        {
#if DEBUG
            data.Add(item.Data);
#endif

            if (string.Equals(item.Data, DoneString, StringComparison.InvariantCulture))
            {
                goto afterStreamEnds;
            }

            ChatResult? res = JsonConvert.DeserializeObject<ChatResult>(item.Data);

            if (res is null)
            {
                continue;
            }

            if (res.Choices?.Count > 0)
            {
                ChatChoice choice = res.Choices[0];

                if (choice.FinishReason is not (null or ChatMessageFinishReasons.Unknown))
                {
                    finishReason = choice.FinishReason ?? ChatMessageFinishReasons.Unknown;
                }
            }

            if (request.StreamOptions?.IncludeUsage ?? false)
            {
                if (usage is null && res.Choices is null || res.Choices?.Count is 0)
                {
                    usage = res.Usage;
                    continue;
                }
            }

            switch (state)
            {
                case ChatStreamParsingStates.Text when res is { Choices.Count: > 0 } && res.Choices[0].Delta?.ToolCalls?.Count > 0:
                {
                    toolsAccumulator = res;
                    toolsMessage     = res.Choices[0].Delta;
                    state            = ChatStreamParsingStates.Tools;

                    if (toolsMessage is not null)
                    {
                        toolsMessage.ToolCallsDict = [];

                        if (toolsMessage.ToolCalls is not null)
                        {
                            foreach (ToolCall toolCall in toolsMessage.ToolCalls)
                            {
                                toolsMessage.ToolCallsDict.TryAdd(toolCall.Index?.ToString() ?? toolCall.Id ?? string.Empty, new ToolCallInboundAccumulator
                                {
                                    ToolCall = toolCall
                                });
                            }
                        }
                    }

                    continue;
                }
                case ChatStreamParsingStates.Text:
                {
                    if (res.Choices is null || res.Choices.Count is 0)
                    {
                        continue;
                    }

                    ChatChoice choice = res.Choices[0];

                    if (choice.Delta is null)
                    {
                        continue;
                    }

                    if (choice.Delta.ReasoningContent is null && choice.Delta.Content is null)
                    {
                        // shouldn't happen but in case of, bail early
                        continue;
                    }

                    choice.Delta.Role = ChatMessageRoles.Assistant;

                    if (choice.Delta.ReasoningContent is not null)
                    {
                        reasoningBuilder ??= new StringBuilder();
                        reasoningBuilder.Append(choice.Delta.ReasoningContent);
                    }

                    if (choice.Delta.Content is not null)
                    {
                        plaintextBuilder ??= new StringBuilder();
                        plaintextBuilder.Append(choice.Delta!.Content);
                    }

                    yield return res;
                    continue;
                }
                case ChatStreamParsingStates.Tools:
                {
                    if (toolsMessage?.ToolCalls is not null && toolsMessage.ToolCallsDict is not null && res.Choices?.Count > 0)
                    {
                        ChatChoice choice = res.Choices[0];

                        if (choice.Delta?.ToolCalls?.Count > 0)
                        {
                            foreach (ToolCall toolCall in choice.Delta.ToolCalls)
                            {
                                string key = toolCall.Index?.ToString() ?? toolCall.Id ?? string.Empty;

                                // we can either encounter a new function or we get a new arguments token
                                if (toolsMessage.ToolCallsDict.TryGetValue(key, out ToolCallInboundAccumulator? accu))
                                {
                                    accu.ArgumentsBuilder.Append(toolCall.FunctionCall.Arguments);
                                }
                                else
                                {
                                    toolsMessage.ToolCalls.Add(toolCall);

                                    toolsMessage.ToolCallsDict.Add(key, new ToolCallInboundAccumulator
                                    {
                                        ToolCall = toolCall
                                    });
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }

        afterStreamEnds:

        if (parseTools && toolsAccumulator is not null && toolsMessage?.ToolCalls is not null && toolsMessage.ToolCallsDict is not null)
        {
            foreach (KeyValuePair<string, ToolCallInboundAccumulator> tool in toolsMessage.ToolCallsDict)
            {
                tool.Value.ToolCall.FunctionCall.Arguments = tool.Value.ArgumentsBuilder.ToString();
            }

            if (toolsAccumulator.Choices is not null)
            {
                toolsAccumulator.Choices[0].FinishReason = ChatMessageFinishReasons.ToolCalls;
            }

            toolsAccumulator.Usage = usage;
            toolsMessage.Role      = ChatMessageRoles.Tool;
            yield return toolsAccumulator;
            yield break;
        }

        string? accuPlaintext      = plaintextBuilder?.ToString();
        string? reasoningPlaintext = reasoningBuilder?.ToString();

        if (accuPlaintext is not null)
        {
            yield return new ChatResult
            {
                Usage = usage,
                Choices =
                [
                    new ChatChoice
                    {
                        Delta = new ChatMessage
                        {
                            Content          = accuPlaintext,
                            ReasoningContent = reasoningPlaintext
                        }
                    }
                ],
                StreamInternalKind = ChatResultStreamInternalKinds.AppendAssistantMessage
            };
        }

        yield return new ChatResult
        {
            Usage = usage,
            Choices =
            [
                new ChatChoice
                {
                    FinishReason = finishReason
                }
            ],
            StreamInternalKind = ChatResultStreamInternalKinds.FinishData
        };
    }

    public override async IAsyncEnumerable<object?> InboundStream(Type type, StreamReader reader)
    {
        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync())
        {
            yield return JsonConvert.DeserializeObject(item.Data, type);
        }
    }

    public override async IAsyncEnumerable<T?> InboundStream<T>(StreamReader reader) where T : class
    {
        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync())
        {
            if (item.Data is "[DONE]")
            {
                continue;
            }

            yield return JsonConvert.DeserializeObject<T>(item.Data);
        }
    }

    public async IAsyncEnumerable<RunStreamEvent> InboundStream(StreamReader reader)
    {
        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync())
        {
            yield return new RunStreamEvent
            {
                Data      = item.Data,
                EventType = item.EventType
            };
        }
    }
}