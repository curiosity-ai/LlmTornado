using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Code;
using LlmTornado.Images;
using LlmTornado.Images.Models;
using LlmTornado.Images.Vendors.Google;
using LlmTornado.Interactions;
using LlmTornado.Models.Vendors.Google;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class GeminiOctober2026Tests
{
    private static IEndpointProvider Provider => new TornadoApi(LLmProviders.Google, "test-key").GetProvider(LLmProviders.Google);

    [Test]
    public void ArgonLongDecode_JoinsAllContinuationHopsAndRecognizesQueuedStatus()
    {
        Interaction interaction = JsonConvert.DeserializeObject<Interaction>("""
            {"model":"gemini-4-argon","status":"queued","steps":[
            {"type":"model_output","content":[{"type":"text","text":"Chapter one. "}]},
            {"type":"function_call","name":"lookup","arguments":{}},
            {"type":"model_output","content":[{"type":"text","text":"Chapter two."}]}]}
            """)!;
        Assert.That(interaction.StatusEnum, Is.EqualTo(InteractionStatus.Queued));
        Assert.That(interaction.OutputText, Is.EqualTo("Chapter one. Chapter two."));
        Assert.That(interaction.OutputTextAll, Is.EqualTo("Chapter one. Chapter two."));
        interaction.Model = "gemini-3.8-flash";
        Assert.That(interaction.OutputText, Is.EqualTo("Chapter two."));
    }

    [Test]
    public void Argon_DefaultThinkingUsesMediumAndRejectsExplicitCacheReference()
    {
        ChatRequest request = new()
        {
            Model = "gemini-4-argon",
            ReasoningBudget = 4096,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")]
        };
        JObject body = JObject.Parse(request.Serialize(Provider).Body.ToString()!);
        Assert.That(body["generationConfig"]?["thinkingConfig"]?["thinkingLevel"]?.ToString(), Is.EqualTo("medium"));
        Assert.That(body["generationConfig"]?["thinkingConfig"]?["thinkingBudget"], Is.Null);
        request.VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions { CachedContent = "cachedContents/123" });
        Assert.Throws<ArgumentException>(() => request.Serialize(Provider));
    }

    [TestCase("gemini-nano-banana-2.1")]
    [TestCase("gemini-3.1-flash-lite-image")]
    public void NativeImageGeneration_UsesGenerateContentWithoutRequiringVideo(string model)
    {
        ImageGenerationRequest request = new("Draw a tree")
        {
            Model = model,
            VendorExtensions = new(new ImageGenerationRequestGoogleExtensions
            {
                ImageSize = ImageGenerationRequestGoogleExtensionsImageSizes.Resolution2K,
                AspectRatio = ImageGenerationRequestGoogleExtensionsAspectRatios.Landscape8x1,
                ThinkingLevel = "medium"
            })
        };
        var serialized = request.Serialize(Provider);
        Assert.That(serialized.Url, Does.EndWith($"/{model}:generateContent"));
        JObject body = JObject.Parse(serialized.Body.ToString()!);
        Assert.That(body["contents"]?[0]?["parts"]?[0]?["text"]?.ToString(), Is.EqualTo("Draw a tree"));
        Assert.That(body["instances"], Is.Null);
        Assert.That(body["generationConfig"]?["responseModalities"]?.Values<string>(), Does.Contain("IMAGE"));
        Assert.That(body["generationConfig"]?["imageConfig"]?["imageSize"]?.ToString(), Is.EqualTo("2K"));
        Assert.That(body["generationConfig"]?["imageConfig"]?["aspectRatio"]?.ToString(), Is.EqualTo("8:1"));
    }

    [Test]
    public void NativeImageEditing_SendsAllSourceImagesInlineAndParsesImageResponse()
    {
        ImageEditRequest request = new("Combine the images")
        {
            Model = ImageModel.Google.Gemini.GeminiNanoBanana21,
            Images = [new("data:image/png;base64,AQID", "image/png"), new("BAUG", "image/jpeg")]
        };
        var serialized = request.Serialize(Provider);
        Assert.That(serialized.Url, Does.EndWith("/gemini-nano-banana-2.1:generateContent"));
        JObject body = JObject.Parse(serialized.Body.ToString()!);
        Assert.That(body["contents"]?[0]?["parts"]?[0]?["inlineData"]?["data"]?.ToString(), Is.EqualTo("AQID"));
        Assert.That(body["contents"]?[0]?["parts"]?[1]?["inlineData"]?["mimeType"]?.ToString(), Is.EqualTo("image/jpeg"));
        var result = Provider.InboundMessage<ImageGenerationResult>("""
            {"candidates":[{"content":{"role":"model","parts":[{"text":"Here it is"},{"inlineData":{"mimeType":"image/png","data":"AQID"}}]}}]}
            """, null, request)!;
        Assert.That(result.Data, Has.Count.EqualTo(1));
        Assert.That(result.Data![0].Base64, Is.EqualTo("AQID"));
    }

    [Test]
    public void OctoberDeprecations_ExposeReplacementModels()
    {
        Assert.That(ChatModel.Google.Gemini.Gemini37Flash.GoogleLifecycle!.Stage, Is.EqualTo(GoogleModelStage.Deprecated));
        Assert.That(ChatModel.Google.Gemini.Gemini37Flash.GoogleLifecycle.ReplacementModel, Is.EqualTo("gemini-3.8-flash"));
        Assert.That(ChatModel.Google.Gemini.Gemini35Flash.GoogleLifecycle!.ReplacementModel, Is.EqualTo("gemini-3.6-flash"));
        Assert.That(ChatModel.Google.Gemini.Gemini31FlashImage.GoogleLifecycle!.ReplacementModel, Is.EqualTo("gemini-nano-banana-2.1"));
    }
}
