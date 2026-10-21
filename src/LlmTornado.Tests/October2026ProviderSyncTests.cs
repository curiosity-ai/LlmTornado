using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Images.Models;
using LlmTornado.Images.Models.Google;
using LlmTornado.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class October2026ProviderSyncTests
{
    private static JObject Serialize(ChatRequest request, LLmProviders provider) =>
        JObject.Parse(request.Serialize(new TornadoApi(provider, "test-key").GetProvider(provider)).Body.ToString()!);

    [TestCase("claude-opus-5-5")]
    [TestCase("claude-sonnet-5-5")]
    [TestCase("claude-haiku-5-5")]
    public void Claude55_RegisteredAndUsesAdaptiveThinking(string model)
    {
        Assert.That(ChatModel.Anthropic.AllModels.Any(m => m.Name == model && ((ChatModel)m).ContextTokens == 1_000_000), Is.True);
        JObject body = Serialize(new ChatRequest
        {
            Model = model,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            Temperature = 0.2,
            TopP = 0.7,
            ReasoningBudget = 2048
        }, LLmProviders.Anthropic);
        Assert.That(body["thinking"]?["type"]?.ToString(), Is.EqualTo("adaptive"));
        Assert.That(body["thinking"]?["budget_tokens"], Is.Null);
        Assert.That(body["temperature"], Is.Null);
        Assert.That(body["top_p"], Is.Null);
    }

    [TestCase("claude-opus-5-5", null)]
    [TestCase("claude-sonnet-5-5", "between_tools")]
    [TestCase("claude-haiku-5-5", "disabled")]
    public void Claude55_DisabledThinkingUsesSupportedMode(string model, string? expected)
    {
        JObject body = Serialize(new ChatRequest
        {
            Model = model,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Effort = AnthropicEffortLevels.Max,
                Thinking = new AnthropicThinkingSettings { Type = AnthropicThinkingTypes.Disabled }
            })
        }, LLmProviders.Anthropic);
        Assert.That(body["thinking"]?["type"]?.ToString(), Is.EqualTo(expected));
        Assert.That(body["output_config"]?["effort"]?.ToString(), Is.EqualTo(expected is null ? "max" : "high"));
    }

    [TestCase("claude-opus-5-5")]
    [TestCase("claude-sonnet-5-5")]
    public void Claude55_ForcedToolChoiceUsesAuto(string model)
    {
        JObject body = Serialize(new ChatRequest
        {
            Model = model,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            Tools = [new Tool(new ToolFunction("lookup", "Look up a record"))],
            ToolChoice = OutboundToolChoice.Required
        }, LLmProviders.Anthropic);
        Assert.That(body["tool_choice"]?["type"]?.ToString(), Is.EqualTo("auto"));
    }

    [Test]
    public void Sonnet55_BetweenToolsHasNoAdditionalThinkingFields()
    {
        JObject body = Serialize(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude5.Sonnet55,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorAnthropicExtensions
            {
                Thinking = new AnthropicThinkingSettings
                {
                    Type = AnthropicThinkingTypes.BetweenTools,
                    Display = AnthropicThinkingDisplay.Summarized,
                    BudgetTokens = 2048
                }
            })
        }, LLmProviders.Anthropic);
        Assert.That(body["thinking"]!.ToString(Formatting.None), Is.EqualTo("{\"type\":\"between_tools\"}"));
    }

    [TestCase("gemini-4-argon", ChatReasoningEfforts.Medium, "medium")]
    [TestCase("gemini-4-argon", ChatReasoningEfforts.Minimal, "low")]
    [TestCase("gemini-nano-banana-2.1", ChatReasoningEfforts.Low, "minimal")]
    [TestCase("gemini-nano-banana-2.1", ChatReasoningEfforts.High, "high")]
    public void NewGeminiModels_MapThinkingLevels(string model, ChatReasoningEfforts effort, string expected)
    {
        Assert.That(ChatModel.Google.AllModels.Any(m => m.Name == model), Is.True);
        JObject body = Serialize(new ChatRequest
        {
            Model = model,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            ReasoningEffort = effort
        }, LLmProviders.Google);
        Assert.That(body["generationConfig"]?["thinkingConfig"]?["thinkingLevel"]?.ToString(), Is.EqualTo(expected));
        Assert.That(body["generationConfig"]?["thinkingConfig"]?["thinkingBudget"], Is.Null);
    }

    [Test]
    public void NewGeminiMediaModels_AreRegisteredForTheirModalities()
    {
        Assert.That(ChatModelGoogle.TtsModels, Does.Contain(ChatModel.Google.Gemini.Gemini38FlashTts));
        Assert.That(ChatModelGoogle.TtsModels, Does.Contain(ChatModel.Google.Gemini.Gemini38FlashLiteTts));
        Assert.That(ChatModelGoogle.ImageModalitySupportingModels, Does.Contain(ChatModel.Google.Gemini.GeminiNanoBanana21));
        Assert.That(ImageModel.AllModelsMap.ContainsKey("gemini-nano-banana-2.1"), Is.True);
        Assert.That(ImageModelGoogleGemini.SupportsVideoToImage("gemini-nano-banana-2.1"), Is.True);
    }

    [TestCase("input_tokens_details", "output_tokens_details")]
    [TestCase("input_token_details", "output_token_details")]
    public void Responses_UsageReadsCacheWritesAndReasoning(string inputField, string outputField)
    {
        string json = new JObject
        {
            [inputField] = new JObject { ["cached_tokens"] = 12000, ["cache_write_tokens"] = 3000 },
            [outputField] = new JObject { ["reasoning_tokens"] = 100 }
        }.ToString();
        ResponseUsage usage = JsonConvert.DeserializeObject<ResponseUsage>(json)!;
        Assert.That(usage.InputTokenDetails!.CachedTokens, Is.EqualTo(12000));
        Assert.That(usage.InputTokenDetails.CacheWriteTokens, Is.EqualTo(3000));
        Assert.That(usage.OutputTokenDetails!.ReasoningTokens, Is.EqualTo(100));
    }

    [Test]
    public void Responses_CacheBreakpointSurvivesContentConversion()
    {
        ResponseInputContent content = new ResponseInputContentText("Shared instructions")
        {
            PromptCacheBreakpoint = new ResponsePromptCacheBreakpoint { Ttl = ResponsePromptCacheTtl.TwelveHours }
        };
        string json = JsonConvert.SerializeObject(content);
        Assert.That(JObject.Parse(json)["prompt_cache_breakpoint"]?["ttl"]?.ToString(), Is.EqualTo("12h"));
        Assert.That(JsonConvert.DeserializeObject<ResponseInputContent>(json)!.PromptCacheBreakpoint!.Ttl,
            Is.EqualTo(ResponsePromptCacheTtl.TwelveHours));
    }

    [Test]
    public void AnthropicModels_PreservesNewCapabilities()
    {
        var model = LlmTornado.Models.Vendors.Anthropic.VendorAnthropicRetrievedModelsDeserializer.DeserializeModel(
            """
            {"id":"claude-sonnet-5-5","line":"sonnet","capabilities":{
              "code_execution":{"supported":false},
              "server_tools":{"code_execution":{"supported":true},"web_search":{"supported":true}},
              "thinking":{"supported":true,"types":{"disabled":{"supported":false},"between_tools":{"supported":true}}}
            }}
            """, null)!;
        Assert.That(model.Line, Is.EqualTo("sonnet"));
        Assert.That(model.Capabilities!.CodeExecution!.Supported, Is.False);
        Assert.That(model.Capabilities.ServerTools!.CodeExecution!.Supported, Is.True);
        Assert.That(model.Capabilities.Thinking!.Types!.BetweenTools!.Supported, Is.True);
        Assert.That(model.Capabilities.Thinking.Types.Disabled!.Supported, Is.False);
    }

    [Test]
    public void Responses_TwelveHourCaching_RoundTrips()
    {
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt6.V61Sol,
            InputString = "Hello",
            PromptCacheOptions = new ResponsePromptCacheOptions { Ttl = ResponsePromptCacheTtl.TwelveHours }
        };
        JObject body = JObject.Parse(request.Serialize(new TornadoApi("test-key").GetProvider(LLmProviders.OpenAi)).Body.ToString()!);
        Assert.That(body["prompt_cache_options"]?["ttl"]?.ToString(), Is.EqualTo("12h"));
        Assert.That(JsonConvert.DeserializeObject<ResponseResult>("{\"prompt_cache_options\":{\"ttl\":\"12h\"}}")!.PromptCacheOptions!.Ttl,
            Is.EqualTo(ResponsePromptCacheTtl.TwelveHours));
    }
}
