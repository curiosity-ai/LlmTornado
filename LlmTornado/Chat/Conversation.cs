﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Code.Vendor;
using LlmTornado.Files;

namespace LlmTornado.Chat;

/// <summary>
///     Represents on ongoing chat with back-and-forth interactions between the user and the chatbot.
///     This is the simplest way to interact with the Chat APIs, rather than manually using the ChatEndpoint methods.
/// </summary>
public class Conversation
{
    /// <summary>
    ///     An internal reference to the API endpoint, needed for API requests
    /// </summary>
    private readonly ChatEndpoint endpoint;

    /// <summary>
    ///     An internal handle to the messages currently enlisted in the conversation.
    /// </summary>
    private readonly List<ChatMessage> messages;

    /// <summary>
    ///     Creates a new conversation.
    /// </summary>
    /// <param name="endpoint">
    ///     A reference to the API endpoint, needed for API requests. Generally should be
    ///     <see cref="TornadoApi.Chat" />.
    /// </param>
    /// <param name="model">
    ///     Optionally specify the model to use for Chat requests. If not specified, used
    ///     <paramref name="defaultChatRequestArgs" />.Model or falls back to <see cref="LlmTornado.Models.Model.GPT35_Turbo" />
    /// </param>
    /// <param name="defaultChatRequestArgs">
    ///     Allows setting the parameters to use when calling the Chat API. Can be useful for setting temperature,
    ///     presence_penalty, and more.  See
    ///     <see href="https://platform.openai.com/docs/api-reference/chat/create">
    ///         OpenAI documentation for a list of possible
    ///         parameters to tweak.
    ///     </see>
    /// </param>
    public Conversation(ChatEndpoint endpoint, ChatModel? model = null, ChatRequest? defaultChatRequestArgs = null)
    {
        RequestParameters = new ChatRequest(this, defaultChatRequestArgs);

        if (model is not null)
        {
            RequestParameters.Model = model;
        }

        RequestParameters.Model ??= ChatModel.OpenAi.Gpt35.Turbo;

        messages      = [];
        this.endpoint = endpoint;
    }

    /// <summary>
    ///     Allows setting the parameters to use when calling the Chat API.
    /// </summary>
    public ChatRequest RequestParameters { get; }

    /// <summary>
    ///     Specifies the model to use for Chat requests. This is just a shorthand to access
    ///     <see cref="RequestParameters" />.Model
    /// </summary>
    public ChatModel Model
    {
        get => RequestParameters.Model ?? ChatModel.OpenAi.Gpt35.Turbo;
        set => RequestParameters.Model = value;
    }

    /// <summary>
    ///     Called after one or more tools are requested by the model and the corresponding results are resolved.
    /// </summary>
    public Func<ResolvedToolsCall, Task>? OnAfterToolsCall { get; set; }

    /// <summary>
    ///     After calling <see cref="GetResponse" />, this contains the full response object which can contain
    ///     useful metadata like token usages, <see cref="ChatChoice.FinishReason" />, etc.  This is overwritten with every
    ///     call to <see cref="GetResponse" /> and only contains the most recent result.
    /// </summary>
    public ChatResult? MostRecentApiResult { get; private set; }

    /// <summary>
    ///     If not null, overrides the default OpenAI auth
    /// </summary>
    public ApiAuthentication? Auth { get; set; }

    /// <summary>
    ///     A list of messages exchanged so far.  Do not modify this list directly.  Instead, use
    ///     <see cref="AppendMessage(ChatMessage)" />, <see cref="AppendUserInput(string)" />,
    ///     <see cref="AppendSystemMessage(string)" />, or <see cref="AppendExampleChatbotOutput(string)" />.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages => messages.ToList();

    /// <summary>
    ///     Appends a <see cref="ChatMessage" /> to the chat history
    /// </summary>
    /// <param name="message">The <see cref="ChatMessage" /> to append to the chat history</param>
    public Conversation AppendMessage(ChatMessage message)
    {
        messages.Add(message);
        return this;
    }

    /// <summary>
    ///     Appends a <see cref="ChatMessage" /> to the chat hstory
    /// </summary>
    /// <param name="message">The <see cref="ChatMessage" /> to append to the chat history</param>
    /// <param name="position">Zero-based index at which to insert the message</param>
    public Conversation AppendMessage(ChatMessage message, int position)
    {
        messages.Insert(position, message);
        return this;
    }

    /// <summary>
    ///     Removes given message from the conversation. If the message is not found, nothing happens
    /// </summary>
    /// <param name="message"></param>
    /// <returns>Whether message was removed</returns>
    public bool RemoveMessage(ChatMessage message)
    {
        ChatMessage? msg = messages.FirstOrDefault(x => x.Id == message.Id);

        if (msg is not null)
        {
            messages.Remove(msg);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Removes message with given id from the conversation. If the message is not found, nothing happens
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Whether message was removed</returns>
    public bool RemoveMessage(Guid id)
    {
        ChatMessage? msg = messages.FirstOrDefault(x => x.Id == id);

        if (msg is not null)
        {
            messages.Remove(msg);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Updates text of a given message
    /// </summary>
    /// <param name="message">Message to update</param>
    /// <param name="content">New text</param>
    public Conversation EditMessageContent(ChatMessage message, string content)
    {
        message.Content = content;
        message.Parts   = null;
        return this;
    }

    /// <summary>
    ///     Updates parts of a given message
    /// </summary>
    /// <param name="message">Message to update</param>
    /// <param name="parts">New parts</param>
    public Conversation EditMessageContent(ChatMessage message, IEnumerable<ChatMessagePart> parts)
    {
        message.Content = null;
        message.Parts   = parts.ToList();
        return this;
    }

    /// <summary>
    ///     Finds a message in the conversation by id. If found, updates text of this message
    /// </summary>
    /// <param name="id">Message to update</param>
    /// <param name="content">New text</param>
    /// <returns>Whether message was updated</returns>
    public bool EditMessageContent(Guid id, string content)
    {
        ChatMessage? msg = messages.FirstOrDefault(x => x.Id == id);

        if (msg is not null)
        {
            msg.Content = content;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Updates role of a given message
    /// </summary>
    /// <param name="message">Message to update</param>
    /// <param name="role">New role</param>
    public Conversation EditMessageRole(ChatMessage message, ChatMessageRoles role)
    {
        message.Role = role;
        return this;
    }

    /// <summary>
    ///     Finds a message in the conversation by id. If found, updates text of this message
    /// </summary>
    /// <param name="id">Message to update</param>
    /// <param name="role">New role</param>
    /// <returns>Whether message was updated</returns>
    public bool EditMessageRole(Guid id, ChatMessageRoles role)
    {
        ChatMessage? msg = messages.FirstOrDefault(x => x.Id == id);

        if (msg is not null)
        {
            msg.Role = role;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history
    /// </summary>
    /// <param name="role">
    ///     The <see cref="ChatMessageRoles" /> for the message.  Typically, a conversation is formatted with a
    ///     system message first, followed by alternating user and assistant messages.  See
    ///     <see href="https://platform.openai.com/docs/guides/chat/introduction">the OpenAI docs</see> for more details about
    ///     usage.
    /// </param>
    /// <param name="content">The content of the message)</param>
    public Conversation AppendMessage(ChatMessageRoles role, string content)
    {
        return AppendMessage(new ChatMessage(role, content));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat hstory
    /// </summary>
    /// <param name="role">
    ///     The <see cref="ChatMessageRoles" /> for the message.  Typically, a conversation is formatted with a
    ///     system message first, followed by alternating user and assistant messages.  See
    ///     <see href="https://platform.openai.com/docs/guides/chat/introduction">the OpenAI docs</see> for more details about
    ///     usage.
    /// </param>
    /// <param name="content">The content of the message</param>
    /// <param name="id">Id of the message</param>
    public Conversation AppendMessage(ChatMessageRoles role, string content, Guid? id)
    {
        return AppendMessage(new ChatMessage(role, content, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="content">
    ///     Text content generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    public Conversation AppendUserInput(string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, content));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="content">
    ///     Text content generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AppendUserInput(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, content, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="parts">
    ///     Parts of the message
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AppendUserInput(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, parts, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="parts">
    ///     Parts of the message
    /// </param>
    public Conversation AppendUserInput(IEnumerable<ChatMessagePart> parts)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, parts));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="userName">The name of the user in a multi-user chat</param>
    /// <param name="content">
    ///     Text content generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    public Conversation AppendUserInputWithName(string userName, string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, content) { Name = userName });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="userName">The name of the user in a multi-user chat</param>
    /// <param name="content">
    ///     Text content generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AppendUserInputWithName(string userName, string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, content, id) { Name = userName });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="userName">The name of the user in a multi-user chat</param>
    /// <param name="parts">
    ///     Parts of the message generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AppendUserInputWithName(string userName, IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, parts, id) { Name = userName });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />. The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="content">text content that helps set the behavior of the assistant</param>
    public Conversation AppendSystemMessage(string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, content));
    }

    /// <summary>
    ///      Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///      <see cref="ChatMessageRoles.System" />. The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="parts"></param>
    /// <returns></returns>
    public Conversation AppendSystemMessage(IEnumerable<ChatMessagePart> parts)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, parts));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="content">text content that helps set the behavior of the assistant</param>
    /// <param name="id">id of the message</param>
    public Conversation AppendSystemMessage(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, content, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="parts">Parts of the message which helps set the behavior of the assistant</param>
    /// <param name="id">id of the message</param>
    public Conversation AppendSystemMessage(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, parts, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="content">text content that helps set the behavior of the assistant</param>
    /// <param name="id">id of the message</param>
    public Conversation PrependSystemMessage(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, content, id), 0);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="parts">Parts of the message which helps set the behavior of the assistant</param>
    /// <param name="id">id of the message</param>
    public Conversation PrependSystemMessage(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, parts, id), 0);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="content">Text content written by a developer to help give examples of desired behavior</param>
    public Conversation AppendExampleChatbotOutput(string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Tool" />.  The function message is a response to a request from the system for
    ///     output from a predefined function.
    /// </summary>
    /// <param name="functionName">The name of the function for which the content has been generated as the result</param>
    /// <param name="content">The text content (usually JSON)</param>
    public Conversation AppendFunctionMessage(string functionName, string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Tool, content) { Name = functionName });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="content">Text content written by a developer to help give examples of desired behavior</param>
    /// <param name="id">id of the message</param>
    public Conversation AppendExampleChatbotOutput(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="parts">Parts of the message written by a developer to help give examples of desired behavior</param>
    /// <param name="id">id of the message</param>
    public Conversation AppendExampleChatbotOutput(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, parts, id));
    }

#region Non-streaming

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />.
    /// </summary>
    /// <returns>The string of the response from the chatbot API</returns>
    public async Task<string?> GetResponse(CancellationToken token = default)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages          = messages,
            CancellationToken = token
        };

        ChatResult? res = await endpoint.CreateChatCompletion(req);

        if (res is null) return null;

        MostRecentApiResult = res;

        if (res.Choices is null) return null;

        if (res.Choices.Count > 0)
        {
            ChatMessage? newMsg = res.Choices[0].Message;

            if (newMsg is not null)
            {
                AppendMessage(newMsg);
            }

            return newMsg?.Content;
        }

        return null;
    }

    /// <summary>
    ///     Calls the API to get a response. The response is split into multiple blocks.
    ///     Unlike <see cref="GetResponse"/> the returned object also contains vendor specific extensions.
    ///     Use this function to get more details about the returned data.
    /// </summary>
    /// <returns>The response from the chatbot API</returns>
    public async Task<ChatRichResponse> GetResponseRich(CancellationToken token = default)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages          = messages,
            CancellationToken = token
        };

        ChatResult? res = await endpoint.CreateChatCompletion(req);

        if (res is null)
        {
            return new ChatRichResponse(null, null);
        }

        MostRecentApiResult = res;

        if (res.Choices is null)
        {
            return new ChatRichResponse(res, null);
        }

        ChatRichResponse response = await HandleResponseRich(res, null);
        return response;
    }


    /// <summary>
    ///     Calls the API to get a response. The response is split into text & tools blocks.
    ///     The entire response is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />. This method doesn't throw on network level.
    /// </summary>
    /// <returns>The string of the response from the chatbot API</returns>
    public async Task<RestDataOrException<ChatRichResponse>> GetResponseRichSafe(CancellationToken token = default)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages          = messages,
            CancellationToken = token
        };

        HttpCallResult<ChatResult> res = await endpoint.CreateChatCompletionSafe(req);

        if (!res.Ok)
        {
            return new RestDataOrException<ChatRichResponse>(res);
        }

        MostRecentApiResult = res.Data;

        if (res.Data.Choices is null)
        {
            return new RestDataOrException<ChatRichResponse>(new Exception("The service returned no choices"), res);
        }

        ChatRichResponse response = await HandleResponseRich(res.Data, null);
        return new RestDataOrException<ChatRichResponse>(response, res);
    }

    /// <summary>
    ///     Calls the API to get a response. Thr response is split into text & tools blocks.
    ///     The entire response is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />.
    ///     Use this overload when resolving the function calls requested by the model can be done immediately.
    ///     This method doesn't throw on network level.
    /// </summary>
    /// <returns>The string of the response from the chatbot API</returns>
    public async Task<RestDataOrException<ChatRichResponse>> GetResponseRichSafe(Func<List<FunctionCall>, Task> functionCallHandler, CancellationToken token = default)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages          = messages,
            CancellationToken = token
        };

        HttpCallResult<ChatResult> res = await endpoint.CreateChatCompletionSafe(req);

        if (!res.Ok)
        {
            return new RestDataOrException<ChatRichResponse>(res);
        }

        MostRecentApiResult = res.Data;

        if (res.Data.Choices is null)
        {
            return new RestDataOrException<ChatRichResponse>(new Exception("The service returned no choices"), res);
        }

        ChatRichResponse response = await HandleResponseRich(res.Data, functionCallHandler);
        return new RestDataOrException<ChatRichResponse>(response, res);
    }

    private async Task<ChatRichResponse> HandleResponseRich(ChatResult? res, Func<List<FunctionCall>, Task>? functionCallHandler)
    {
        List<ChatRichResponseBlock> blocks   = [];
        ChatRichResponse            response = new ChatRichResponse(res, blocks);

        if (res is null || !(res.Choices?.Count > 0))
        {
            return response;
        }

        foreach (ChatChoice choice in res.Choices)
        {
            ChatMessage? newMsg = choice.Message;

            if (newMsg is null)
            {
                continue;
            }

            AppendMessage(newMsg);

            if (newMsg.ToolCalls is { Count: > 0 } && !OutboundToolChoice.OutboundToolChoiceConverter.KnownFunctionNames.Contains(newMsg.ToolCalls[0].FunctionCall.Name))
            {
                ResolvedToolsCall result = new ResolvedToolsCall();

                List<FunctionCall>? calls = newMsg.ToolCalls?.Select(x => new FunctionCall
                {
                    Name      = x.FunctionCall.Name,
                    Arguments = x.FunctionCall.Arguments,
                    ToolCall  = x
                }).ToList();

                if (calls is not null)
                {
                    Guid currentMsgId = Guid.NewGuid();

                    if (functionCallHandler is not null)
                    {
                        await functionCallHandler.Invoke(calls);
                    }

                    foreach (FunctionCall call in calls)
                    {
                        blocks.Add(new ChatRichResponseBlock
                        {
                            Type         = ChatRichResponseBlockTypes.Function,
                            FunctionCall = call
                        });

                        ChatMessage fnResultMsg = new ChatMessage(ChatMessageRoles.Tool, call.Result?.Content ?? "The service returned no data.".ToJson(), Guid.NewGuid())
                        {
                            Id                      = currentMsgId,
                            ToolCallId              = call.ToolCall?.Id                ?? call.Name,
                            ToolInvocationSucceeded = call.Result?.InvocationSucceeded ?? false,
                            ContentJsonType         = call.Result?.ContentJsonType     ?? typeof(string)
                        };

                        currentMsgId = Guid.NewGuid();
                        AppendMessage(fnResultMsg);

                        result.ToolResults.Add(new ResolvedToolCall
                        {
                            Call        = call,
                            Result      = call.Result ?? new FunctionResult(call, null, null, false),
                            ToolMessage = fnResultMsg
                        });
                    }

                    if (OnAfterToolsCall is not null)
                    {
                        await OnAfterToolsCall(result);
                    }
                }
            }

            if (newMsg.Parts?.Count > 0)
            {
                blocks.AddRange(newMsg.Parts.Select(x => new ChatRichResponseBlock(x, newMsg)));
            }
            else if (newMsg.Content is not null)
            {
                blocks.Add(new ChatRichResponseBlock
                {
                    Type    = ChatRichResponseBlockTypes.Message,
                    Message = newMsg.Content,
                    Reasoning = newMsg.ReasoningContent is null
                        ? null
                        : new ChatMessageReasoningData
                        {
                            Content  = newMsg.ReasoningContent,
                            Provider = res.Provider?.Provider ?? LLmProviders.OpenAi
                        }
                });
            }
            else if (newMsg.Audio is not null)
            {
                blocks.Add(new ChatRichResponseBlock
                {
                    Type      = ChatRichResponseBlockTypes.Audio,
                    ChatAudio = newMsg.Audio
                });
            }
        }

        return response;
    }

    /// <summary>
    ///     Calls the API to get a response. Thr response is split into text & tools blocks.
    ///     The entire response is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />.
    ///     Use this overload when resolving the function calls requested by the model can be done immediately.
    /// </summary>
    /// <returns>The string of the response from the chatbot API</returns>
    public async Task<ChatRichResponse> GetResponseRich(Func<List<FunctionCall>, Task>? functionCallHandler, CancellationToken token = default)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages          = messages,
            CancellationToken = token
        };

        ChatResult? res = await endpoint.CreateChatCompletion(req);

        if (res is null)
        {
            return new ChatRichResponse(null, null);
        }

        MostRecentApiResult = res;

        if (res.Choices is null)
        {
            return new ChatRichResponse(res, null);
        }

        return await HandleResponseRich(res, functionCallHandler);
    }

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />.
    /// </summary>
    /// <returns>The string of the response from the chatbot API</returns>
    public async Task<RestDataOrException<ChatChoice>> GetResponseSafe(CancellationToken token = default)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages          = messages,
            CancellationToken = token
        };

        HttpCallResult<ChatResult> res = await endpoint.CreateChatCompletionSafe(req);

        if (!res.Ok)
        {
            return new RestDataOrException<ChatChoice>(res);
        }

        MostRecentApiResult = res.Data;

        if (res.Data.Choices is null)
        {
            return new RestDataOrException<ChatChoice>(new Exception("No choices returned by the service."), res);
        }

        if (res.Data.Choices.Count > 0)
        {
            ChatMessage? newMsg = res.Data.Choices[0].Message;

            if (newMsg is not null)
            {
                AppendMessage(newMsg);
            }

            return new RestDataOrException<ChatChoice>(res.Data.Choices[0], res);
        }

        return new RestDataOrException<ChatChoice>(new Exception("No choices returned by the service."), res);
    }

#endregion

#region Streaming

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />, and streams the results to the
    ///     <paramref name="resultHandler" /> as they come in. <br />
    ///     If you are on the latest C# supporting async enumerables, you may prefer the cleaner syntax of
    ///     <see cref="StreamResponseEnumerable" /> instead.
    /// </summary>
    /// <param name="resultHandler">An action to be called as each new result arrives.</param>
    /// <param name="token"></param>
    public async Task StreamResponse(Action<string> resultHandler, CancellationToken token = default)
    {
        await foreach (string res in StreamResponseEnumerable(token: token))
        {
            resultHandler(res);
        }
    }

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />, and streams the results to the
    ///     <paramref name="resultHandler" /> as they come in. <br />
    ///     If you are on the latest C# supporting async enumerables, you may prefer the cleaner syntax of
    ///     <see cref="StreamResponseEnumerable" /> instead.
    /// </summary>
    /// <param name="resultHandler">
    ///     An action to be called as each new result arrives, which includes the index of the result
    ///     in the overall result set.
    /// </param>
    /// <param name="token"></param>
    public async Task StreamResponse(Action<int, string> resultHandler, CancellationToken token = default)
    {
        int index = 0;

        await foreach (string res in StreamResponseEnumerable(token: token))
        {
            resultHandler(index++, res);
        }
    }

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />, and streams the results as they come in.
    ///     <br />
    ///     If you are not using C# 8 supporting async enumerables or if you are using the .NET Framework, you may need to use
    ///     <see cref="Code.StreamResponse" /> instead.
    /// </summary>
    /// <returns>
    ///     An async enumerable with each of the results as they come in.  See
    ///     <see href="https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#asynchronous-streams" /> for more
    ///     details on how to consume an async enumerable.
    /// </returns>
    public async IAsyncEnumerable<string> StreamResponseEnumerable(Guid? messageId = null, CancellationToken token = default)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages          = messages,
            CancellationToken = token
        };

        StringBuilder     responseStringBuilder = new StringBuilder();
        ChatMessageRoles? responseRole          = null;

        await foreach (ChatResult res in endpoint.StreamChatEnumerable(req).WithCancellation(token))
        {
            if (res.Choices is null)
            {
                yield break;
            }

            if (res.Choices.Count <= 0)
            {
                yield break;
            }

            bool solved = false;

            foreach (ChatChoice choice in res.Choices)
            {
                ChatMessage? internalDelta = choice.Delta;

                if (res.StreamInternalKind is ChatResultStreamInternalKinds.AppendAssistantMessage)
                {
                    if (internalDelta is not null)
                    {
                        internalDelta.Role   = ChatMessageRoles.Assistant;
                        internalDelta.Tokens = res.Usage?.CompletionTokens;
                        AppendMessage(internalDelta);
                    }

                    solved = true;
                    break;
                }
            }

            if (solved)
            {
                continue;
            }

            foreach (ChatChoice choice in res.Choices)
            {
                if (choice.Delta is not null)
                {
                    ChatMessage delta = choice.Delta;

                    if (responseRole is null && delta.Role is not null)
                    {
                        responseRole = delta.Role;
                    }

                    string? deltaContent = delta.Content;

                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        responseStringBuilder.Append(deltaContent);
                        yield return deltaContent;
                    }
                }
            }

            MostRecentApiResult = res;
        }

        if (responseRole is not null)
        {
            AppendMessage((ChatMessageRoles)responseRole, responseStringBuilder.ToString(), messageId);
        }
    }

    /// <summary>
    ///     Stream LLM response as a series of events. The raw events from Provider are abstracted away and only high-level events are reported such as inbound plaintext tokens, complete tool requests, etc.
    /// </summary>
    public async Task StreamResponseRich(Guid msgId, Func<string?, ValueTask>? messageTokenHandler, Func<List<FunctionCall>, ValueTask>? functionCallHandler, Func<ChatMessageRoles, ValueTask>? messageTypeResolvedHandler, Ref<string>? outboundRequest = null, Func<ChatResponseVendorExtensions, ValueTask>? vendorFeaturesHandler = null, CancellationToken token = default)
    {
        await StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler        = messageTokenHandler,
            FunctionCallHandler        = functionCallHandler,
            MessageTypeResolvedHandler = messageTypeResolvedHandler,
            VendorFeaturesHandler      = vendorFeaturesHandler,
            MessageId                  = msgId
        }, token);
    }

    /// <summary>
    ///     Stream LLM response as a series of events. The raw events from Provider are abstracted away and only high-level events are reported such as inbound plaintext tokens, complete tool requests, etc.
    /// </summary>
    public async Task StreamResponseRich(Func<string?, ValueTask>? messageTokenHandler, Func<List<FunctionCall>, ValueTask>? functionCallHandler, Func<ChatMessageRoles, ValueTask>? messageTypeResolvedHandler, Ref<string>? outboundRequest = null, Func<ChatResponseVendorExtensions, ValueTask>? vendorFeaturesHandler = null, CancellationToken token = default)
    {
        await StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler        = messageTokenHandler,
            FunctionCallHandler        = functionCallHandler,
            MessageTypeResolvedHandler = messageTypeResolvedHandler,
            VendorFeaturesHandler      = vendorFeaturesHandler
        }, token);
    }

    /// <summary>
    /// Certain providers (Google) might take some time to process uploaded files. This affects mostly videos. If the conversations contain any messages with File parts, this call verifies they are ready and if not, waits for the ready state.
    /// </summary>
    /// <param name="checkFrequencyMs">The frequency of polling, in ms</param>
    /// <param name="token">Cancellation token</param>
    public async Task<RestDataOrException<bool>> WaitForContentReady(int checkFrequencyMs = 1000, CancellationToken token = default)
    {
        IEndpointProvider provider = endpoint.Api.GetProvider(Model);

        if (provider.Provider is not LLmProviders.Google)
        {
            return new RestDataOrException<bool>(true, (HttpCallRequest?)null);
        }

        List<ChatMessage> toCheck = Messages.Where(x => x.Parts?.Any(y => y.FileLinkData?.State is not FileLinkStates.Active) ?? false).ToList();

        if (toCheck.Count is 0)
        {
            return new RestDataOrException<bool>(true, (HttpCallRequest?)null);
        }

        List<ChatMessagePart> parts = toCheck.SelectMany(x => x.Parts?.Where(y =>
        {
            if (y.FileLinkData is null)
            {
                return false;
            }

            // not files or active files
            if (y.Type is not ChatMessageTypes.FileLink || y.FileLinkData.State is FileLinkStates.Active)
            {
                return false;
            }

            // YouTube videos or other absolute sources
            if (!y.FileLinkData.FileUri.StartsWith(GoogleEndpointProvider.BaseUrl) && Uri.TryCreate(y.FileLinkData.FileUri, UriKind.Absolute, out _))
            {
                return false;
            }

            return true;
        }) ?? []).ToList();

        if (parts.Count is 0)
        {
            return new RestDataOrException<bool>(true, (HttpCallRequest?)null);
        }

        HashSet<ChatMessagePart> pendingParts = new HashSet<ChatMessagePart>(parts);
        int                      maxIters     = checkFrequencyMs < 100 ? 100 : 20;

        try
        {
            while (pendingParts.Count > 0 && maxIters > 0)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                maxIters--;

                // select all pending parts
                Task<(ChatMessagePart part, bool isProcessing)>[] tasks = pendingParts.Select(async part =>
                {
                    if (part.FileLinkData is null)
                    {
                        return (part, isProcessing: false);
                    }

                    TornadoFile? file = await endpoint.Api.Files.Get(part.FileLinkData.FileUri, provider.Provider);

                    bool isProcessing = file?.State is FileLinkStates.Processing;

                    if (!isProcessing)
                    {
                        part.FileLinkData.State = file?.State ?? FileLinkStates.Unknown;

                        if (part.FileLinkData.File is not null)
                        {
                            part.FileLinkData.File.State = file?.State ?? FileLinkStates.Unknown;
                        }
                    }

                    return (part, isProcessing);
                }).ToArray();

                (ChatMessagePart part, bool isProcessing)[] results = await Task.WhenAll(tasks);

                // remove done parts
                foreach ((ChatMessagePart part, bool isProcessing) in results)
                {
                    if (!isProcessing)
                    {
                        pendingParts.Remove(part);
                    }
                }

                if (pendingParts.Count > 0)
                {
                    await Task.Delay(checkFrequencyMs, token);
                }
            }
        }
        catch (Exception e)
        {
            return new RestDataOrException<bool>(e);
        }

        return new RestDataOrException<bool>(true, (HttpCallRequest?)null);
    }

    /// <summary>
    ///     Stream LLM response as a series of events. The raw events from Provider are abstracted away and only high-level events are reported such as inbound plaintext tokens, complete tool requests, etc.
    /// </summary>
    /// <param name="eventsHandler"></param>
    /// <param name="token"></param>
    public async Task StreamResponseRich(ChatStreamEventHandler? eventsHandler, CancellationToken token = default)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages          = messages,
            CancellationToken = token
        };

        IEndpointProvider provider = endpoint.Api.GetProvider(req.Model ?? ChatModel.OpenAi.Gpt35.Turbo);

        req = eventsHandler?.MutateChatRequestHandler is not null ? await eventsHandler.MutateChatRequestHandler.Invoke(req) : req;
        bool         isFirst             = true;
        Guid         currentMsgId        = eventsHandler?.MessageId ?? Guid.NewGuid();
        ChatMessage? lastUserMessage     = messages.LastOrDefault(x => x.Role is ChatMessageRoles.User);
        bool         isFirstMessageToken = true;
        int          tokenIndex          = 0;

        await foreach (ChatResult res in endpoint.StreamChatEnumerable(req, eventsHandler).WithCancellation(token))
        {
            bool solved = false;

            // internal events are resolved immediately, we never return control to the user.
            if (res.StreamInternalKind is not null)
            {
                if (res.Choices is not null)
                {
                    if (res.StreamInternalKind is ChatResultStreamInternalKinds.FinishData)
                    {
                        if (eventsHandler?.OnFinished is not null)
                        {
                            await eventsHandler.OnFinished.Invoke(new ChatStreamFinishedData(res.Usage ?? new ChatUsage(provider.Provider), res.Choices.FirstOrDefault()?.FinishReason ?? ChatMessageFinishReasons.Unknown));
                        }

                        break;
                    }

                    foreach (ChatChoice choice in res.Choices)
                    {
                        ChatMessage? internalDelta = choice.Delta;

                        if (res.StreamInternalKind is ChatResultStreamInternalKinds.AssistantMessageTransientBlock)
                        {
                            if (eventsHandler?.BlockFinishedHandler is not null)
                            {
                                await eventsHandler.BlockFinishedHandler.Invoke(internalDelta);
                            }

                            solved = true;
                            break;
                        }

                        if (res.StreamInternalKind is ChatResultStreamInternalKinds.AppendAssistantMessage)
                        {
                            if (internalDelta is not null)
                            {
                                internalDelta.Role   = ChatMessageRoles.Assistant;
                                internalDelta.Id     = currentMsgId;
                                internalDelta.Tokens = res.Usage?.CompletionTokens;

                                if (lastUserMessage is not null)
                                {
                                    lastUserMessage.Tokens = res.Usage?.PromptTokens;
                                }

                                currentMsgId = Guid.NewGuid();
                                AppendMessage(internalDelta);
                            }

                            if (eventsHandler?.BlockFinishedHandler is not null)
                            {
                                await eventsHandler.BlockFinishedHandler.Invoke(internalDelta);
                            }

                            if (res.Usage is not null && eventsHandler?.OnUsageReceived is not null)
                            {
                                await eventsHandler.OnUsageReceived.Invoke(res.Usage);
                            }

                            solved = true;
                            break;
                        }
                    }
                }
            }

            if (solved)
            {
                continue;
            }

            if (res.Choices is null || res.Choices.Count is 0)
            {
                if (res.VendorExtensions is not null && eventsHandler?.VendorFeaturesHandler is not null)
                {
                    await eventsHandler.VendorFeaturesHandler.Invoke(res.VendorExtensions);
                }

                MostRecentApiResult = res;
                continue;
            }

            foreach (ChatChoice choice in res.Choices)
            {
                ChatMessage? delta   = choice.Delta;
                ChatMessage? message = choice.Message;

                if (isFirst && delta?.Role is not null)
                {
                    if (eventsHandler?.MessageTypeResolvedHandler is not null)
                    {
                        await eventsHandler.MessageTypeResolvedHandler(delta.Role ?? ChatMessageRoles.Unknown);
                    }

                    isFirst = false;
                }

                if (delta is not null && eventsHandler is not null)
                {
                    // role can be either Tool or Assistant, we need to handle both cases
                    if (delta.Role is ChatMessageRoles.Tool || delta.ToolCalls?.Count > 0)
                    {
                        delta.Role = ChatMessageRoles.Assistant;

                        if (eventsHandler.FunctionCallHandler is not null)
                        {
                            ResolvedToolsCall result = new ResolvedToolsCall();

                            List<FunctionCall>? calls = delta.ToolCalls?.Select(x => new FunctionCall
                            {
                                Name      = x.FunctionCall.Name,
                                Arguments = x.FunctionCall.Arguments,
                                ToolCall  = x
                            }).ToList();

                            if (calls is not null)
                            {
                                await eventsHandler.FunctionCallHandler.Invoke(calls);

                                if (MostRecentApiResult?.Choices?.Count > 0 && MostRecentApiResult.Choices[0].FinishReason is ChatMessageFinishReasons.ToolCalls)
                                {
                                    delta.Content = MostRecentApiResult.Object;
                                }

                                if (lastUserMessage is not null)
                                {
                                    lastUserMessage.Tokens = res.Usage?.PromptTokens;
                                }

                                if (res.Usage is not null && eventsHandler.OnUsageReceived is not null)
                                {
                                    await eventsHandler.OnUsageReceived.Invoke(res.Usage);
                                }

                                delta.Tokens            = res.Usage?.CompletionTokens;
                                result.AssistantMessage = delta;
                                AppendMessage(delta);

                                foreach (FunctionCall call in calls)
                                {
                                    ChatMessage fnResultMsg = new ChatMessage(ChatMessageRoles.Tool, call.Result?.Content ?? "The service returned no data.".ToJson(), Guid.NewGuid())
                                    {
                                        Id                      = currentMsgId,
                                        ToolCallId              = call.ToolCall?.Id                ?? call.Name,
                                        ToolInvocationSucceeded = call.Result?.InvocationSucceeded ?? false,
                                        ContentJsonType         = call.Result?.ContentJsonType     ?? typeof(string)
                                    };

                                    currentMsgId = Guid.NewGuid();
                                    AppendMessage(fnResultMsg);

                                    result.ToolResults.Add(new ResolvedToolCall
                                    {
                                        Call        = call,
                                        Result      = call.Result ?? new FunctionResult(call, null, null, false),
                                        ToolMessage = fnResultMsg
                                    });
                                }

                                if (eventsHandler.AfterFunctionCallsResolvedHandler is not null)
                                {
                                    await eventsHandler.AfterFunctionCallsResolvedHandler.Invoke(result, eventsHandler);
                                }

                                if (OnAfterToolsCall is not null)
                                {
                                    await OnAfterToolsCall(result);
                                }
                            }

                            return;
                        }
                    }
                    else if (delta.Role is ChatMessageRoles.Assistant)
                    {
                        if (delta.Parts?.Count > 0)
                        {
                            foreach (ChatMessagePart part in delta.Parts)
                            {
                                if (eventsHandler.MessagePartHandler is not null)
                                {
                                    await InvokeMessagePartHandler(part);
                                }

                                switch (part.Type)
                                {
                                    case ChatMessageTypes.Text when eventsHandler.MessageTokenHandler is not null:
                                    {
                                        await InvokeMessageHandler(part.Text ?? message?.Content);
                                        break;
                                    }
                                    case ChatMessageTypes.Reasoning when eventsHandler.ReasoningTokenHandler is not null:
                                    {
                                        ChatMessageReasoningData? msg = part.Reasoning;

                                        if (RequestParameters.TrimResponseStart && isFirstMessageToken && msg is not null)
                                        {
                                            msg.Content         = msg.Content?.TrimStart();
                                            isFirstMessageToken = false;
                                        }

                                        if (msg is not null)
                                        {
                                            await eventsHandler.ReasoningTokenHandler.Invoke(msg);
                                        }

                                        break;
                                    }
                                    case ChatMessageTypes.Image when eventsHandler.ImageTokenHandler is not null:
                                    {
                                        if (part.Image is not null)
                                        {
                                            await eventsHandler.ImageTokenHandler.Invoke(part.Image);
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (delta.ReasoningContent is not null)
                            {
                                if (eventsHandler.ReasoningTokenHandler is not null)
                                {
                                    await eventsHandler.ReasoningTokenHandler.Invoke(new ChatMessageReasoningData
                                    {
                                        Content  = delta.ReasoningContent,
                                        Provider = provider.Provider
                                    });
                                }
                            }

                            if (eventsHandler.MessagePartHandler is not null)
                            {
                                await InvokeMessagePartHandler(new ChatMessagePart
                                {
                                    Type = ChatMessageTypes.Text,
                                    Text = delta.Content ?? message?.Content
                                });
                            }

                            if (delta.Content is not null)
                            {
                                await InvokeMessageHandler(delta.Content ?? message?.Content);
                            }
                        }

                        if (delta.Audio is not null)
                        {
                            if (eventsHandler.AudioTokenHandler is not null)
                            {
                                await eventsHandler.AudioTokenHandler.Invoke(delta.Audio);
                            }
                        }
                    }
                }
            }

            continue;

            async ValueTask InvokeMessagePartHandler(ChatMessagePart part)
            {
                if (eventsHandler.MessagePartHandler is null)
                {
                    return;
                }

                if (part.Text is not null)
                {
                    if (RequestParameters.TrimResponseStart && isFirstMessageToken)
                    {
                        part.Text           = part.Text.TrimStart();
                        isFirstMessageToken = false;
                    }
                }

                await eventsHandler.MessagePartHandler.Invoke(part);
            }

            async ValueTask InvokeMessageHandler(string? msg)
            {
                if (eventsHandler.MessageTokenExHandler is not null)
                {
                    if (msg is not null)
                    {
                        if (RequestParameters.TrimResponseStart && isFirstMessageToken)
                        {
                            msg                 = msg.TrimStart();
                            isFirstMessageToken = false;
                        }

                        await eventsHandler.MessageTokenExHandler.Invoke(new StreamedMessageToken
                        {
                            Content = msg,
                            Index   = tokenIndex
                        });

                        tokenIndex++;
                    }
                }

                if (eventsHandler.MessageTokenHandler is null)
                {
                    return;
                }

                if (msg is not null)
                {
                    if (RequestParameters.TrimResponseStart && isFirstMessageToken)
                    {
                        msg                 = msg.TrimStart();
                        isFirstMessageToken = false;
                    }

                    await eventsHandler.MessageTokenHandler.Invoke(msg);
                }
            }
        }
    }

#endregion

}