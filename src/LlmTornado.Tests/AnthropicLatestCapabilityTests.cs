using System.Net.Http;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// Serialization tests for Anthropic capabilities added after the May 2026 catalog sync
/// (Opus 5, Fable 5.1, computer/browser toolsets, fallbacks, thinking display updates).
/// </summary>
[TestFixture]
public class AnthropicLatestCapabilityTests
{
    private static IEndpointProvider Provider =>
        new TornadoApi(LLmProviders.Anthropic, "test-key").GetProvider(LLmProviders.Anthropic);

    [Test]
    public void Opus5_ExplicitDisabledThinking_SerializesDisabled()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Opus,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Thinking = new AnthropicThinkingSettings { Type = AnthropicThinkingTypes.Disabled }
            })
        };

        JObject body = ParseBody(request);

        Assert.That(body["thinking"]?["type"]?.ToString(), Is.EqualTo("disabled"));
    }

    [Test]
    public void Sonnet5_ExplicitDisabledThinking_SerializesDisabled()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Sonnet,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Thinking = new AnthropicThinkingSettings { Type = AnthropicThinkingTypes.Disabled }
            })
        };

        JObject body = ParseBody(request);

        Assert.That(body["thinking"]?["type"]?.ToString(), Is.EqualTo("disabled"));
    }

    [Test]
    public void Fable51_DisabledThinking_IsOmitted()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Fable51,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Thinking = new AnthropicThinkingSettings { Type = AnthropicThinkingTypes.Disabled }
            })
        };

        JObject body = ParseBody(request);

        Assert.That(body["thinking"], Is.Null);
    }

    [Test]
    public void Opus5_DisabledThinkingAtMaxEffort_ClampsEffortToHigh()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Opus,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Effort = AnthropicEffortLevels.Max,
                Thinking = new AnthropicThinkingSettings { Type = AnthropicThinkingTypes.Disabled }
            })
        };

        JObject body = ParseBody(request);

        Assert.That(body["thinking"]?["type"]?.ToString(), Is.EqualTo("disabled"));
        Assert.That(body["output_config"]?["effort"]?.ToString(), Is.EqualTo("high"));
    }

    [Test]
    public void Fable51_ForcedToolChoice_RemappedToAuto()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Fable51,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            Tools = [new Tool(new ToolFunction("lookup", "Look something up"))],
            ToolChoice = OutboundToolChoice.Required
        };

        JObject body = ParseBody(request);

        Assert.That(body["tool_choice"]?["type"]?.ToString(), Is.EqualTo("auto"));
    }

    [Test]
    public void ThinkingDisplayUpdates_SerializesAndAddsBetaHeader()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Fable51,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Thinking = new AnthropicThinkingSettings
                {
                    Type = AnthropicThinkingTypes.Adaptive,
                    Display = AnthropicThinkingDisplay.Updates
                }
            })
        };

        JObject body = ParseBody(request);
        Assert.That(body["thinking"]?["display"]?.ToString(), Is.EqualTo("updates"));

        string? beta = BetaHeader(request);
        Assert.That(beta, Does.Contain("thinking-display-updates-2026-08-18"));
    }

    [Test]
    public void ThinkingBlockBinding_SerializesAndAddsBetaHeader()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Fable51,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Thinking = new AnthropicThinkingSettings
                {
                    Type = AnthropicThinkingTypes.Adaptive,
                    BlockBinding = new AnthropicThinkingBlockBinding
                    {
                        PrefixMismatchBehavior = AnthropicThinkingPrefixMismatchBehavior.Drop
                    }
                }
            })
        };

        JObject body = ParseBody(request);
        Assert.That(body["thinking"]?["block_binding"]?["prefix_mismatch_behavior"]?.ToString(), Is.EqualTo("drop_block"));

        string? beta = BetaHeader(request);
        Assert.That(beta, Does.Contain("thinking-binding-controls-2026-08-01"));
    }

    [Test]
    public void FallbacksDefault_SerializesAndAddsBetaHeader()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Opus,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Fallbacks = AnthropicFallbacks.Default
            })
        };

        JObject body = ParseBody(request);
        Assert.That(body["fallbacks"]?.ToString(), Is.EqualTo("default"));

        string? beta = BetaHeader(request);
        Assert.That(beta, Does.Contain("server-side-fallback-2026-07-01"));
    }

    [Test]
    public void FallbacksExplicitModels_SerializesAsArray()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Fable,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Fallbacks = AnthropicFallbacks.FromModels("claude-opus-5", "claude-sonnet-5")
            })
        };

        JObject body = ParseBody(request);
        Assert.That(body["fallbacks"]?.Type, Is.EqualTo(JTokenType.Array));
        Assert.That(body["fallbacks"]![0]?.ToString(), Is.EqualTo("claude-opus-5"));
        Assert.That(body["fallbacks"]![1]?.ToString(), Is.EqualTo("claude-sonnet-5"));
    }

    [Test]
    public void ComputerToolset_SerializesWithoutName()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Opus,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                BuiltInTools = [new VendorAnthropicChatRequestBuiltInToolComputerToolset20260801()]
            })
        };

        JObject body = ParseBody(request);
        JToken? tool = body["tools"]?[0];

        Assert.That(tool?["type"]?.ToString(), Is.EqualTo("computer_toolset_20260801"));
        Assert.That(tool?["name"], Is.Null);
    }

    [Test]
    public void BrowserToolset_SerializesWithoutName()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Sonnet,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                BuiltInTools = [new VendorAnthropicChatRequestBuiltInToolBrowserToolset20260801()]
            })
        };

        JObject body = ParseBody(request);
        JToken? tool = body["tools"]?[0];

        Assert.That(tool?["type"]?.ToString(), Is.EqualTo("browser_toolset_20260801"));
        Assert.That(tool?["name"], Is.Null);
    }

    [Test]
    public void WebSearch20260318_SerializesResponseInclusion()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Opus,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                BuiltInTools =
                [
                    new VendorAnthropicChatRequestBuiltInToolWebSearch20260318
                    {
                        ResponseInclusion = "none"
                    }
                ]
            })
        };

        JObject body = ParseBody(request);
        JToken? tool = body["tools"]?[0];

        Assert.That(tool?["type"]?.ToString(), Is.EqualTo("web_search_20260318"));
        Assert.That(tool?["response_inclusion"]?.ToString(), Is.EqualTo("none"));
    }

    [Test]
    public void CodeExecution20260120_SerializesType()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Opus,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                BuiltInTools = [new VendorAnthropicChatRequestBuiltInToolCodeExecution20260120()]
            })
        };

        JObject body = ParseBody(request);
        Assert.That(body["tools"]?[0]?["type"]?.ToString(), Is.EqualTo("code_execution_20260120"));
        Assert.That(body["tools"]?[0]?["name"]?.ToString(), Is.EqualTo("code_execution"));
    }

    [Test]
    public void MidConversationSystem_ClearAtAndEffort_SerializeWithBetaHeaders()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Opus,
            Messages =
            [
                new ChatMessage(ChatMessageRoles.User, "Hello"),
                new ChatMessage(ChatMessageRoles.Assistant, "Hi."),
                new ChatMessage(ChatMessageRoles.User, "Continue."),
                new ChatMessage(ChatMessageRoles.System, "Be brief.")
                {
                    VendorExtensions = new ChatMessageVendorExtensionsAnthropic
                    {
                        Effort = AnthropicEffortLevels.Low,
                        ClearAt = AnthropicSystemClearAt.NextUserMessage
                    }
                }
            ]
        };

        JObject body = ParseBody(request);
        JToken? systemMessage = body["messages"]?.Last;

        Assert.That(systemMessage?["role"]?.ToString(), Is.EqualTo("system"));
        Assert.That(systemMessage?["clear_at"]?.ToString(), Is.EqualTo("next_user_message"));
        Assert.That(systemMessage?["output_config"]?["effort"]?.ToString(), Is.EqualTo("low"));

        string? beta = BetaHeader(request);
        Assert.That(beta, Does.Contain("mid-conversation-output-config-2026-07-01"));
        Assert.That(beta, Does.Contain("mid-conversation-system-clear-at-2026-08-21"));
    }

    [Test]
    public void MidConversationToolChanges_AddsBetaHeader()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Opus,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                EnableMidConversationToolChanges = true
            })
        };

        string? beta = BetaHeader(request);
        Assert.That(beta, Does.Contain("mid-conversation-tool-changes-2026-07-01"));
    }

    private static JObject ParseBody(ChatRequest request)
    {
        TornadoRequestContent serialized = request.Serialize(Provider);
        return JObject.Parse(serialized.Body.ToString()!);
    }

    private static string? BetaHeader(ChatRequest request)
    {
        AnthropicEndpointProvider provider = new AnthropicEndpointProvider
        {
            Api = new TornadoApi(LLmProviders.Anthropic, "test-key")
        };

        HttpRequestMessage httpRequest = provider.OutboundMessage(
            "https://api.anthropic.com/v1/messages",
            HttpMethod.Post,
            "{}",
            false,
            request);

        return httpRequest.Headers.TryGetValues("anthropic-beta", out IEnumerable<string>? values)
            ? string.Join(",", values)
            : null;
    }
}
