using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Mistral;
using LlmTornado.Code;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class MistralChatSeedTests
{
    private static IEndpointProvider Provider => new TornadoApi(LLmProviders.Mistral, "test-key").GetProvider(LLmProviders.Mistral);

    private static JObject Serialize(ChatRequest request)
    {
        TornadoRequestContent serialized = request.Serialize(Provider);
        return JObject.Parse(serialized.Body.ToString()!);
    }

    private static ChatRequest CreateRequest()
    {
        return new ChatRequest
        {
            Model = ChatModel.Mistral.Premier.MistralLarge,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")]
        };
    }

    [Test]
    public void Seed_IsSentAsRandomSeed()
    {
        ChatRequest request = CreateRequest();
        request.Seed = 42;

        JObject body = Serialize(request);

        Assert.That(body["random_seed"]?.Value<int>(), Is.EqualTo(42));
        Assert.That(body.ContainsKey("seed"), Is.False);
    }

    [Test]
    public void VendorExtensionRandomSeed_WinsOverSeed()
    {
        ChatRequest request = CreateRequest();
        request.Seed = 42;
        request.VendorExtensions = new ChatRequestVendorExtensions
        {
            Mistral = new ChatRequestVendorMistralExtensions { RandomSeed = 7 }
        };

        JObject body = Serialize(request);

        Assert.That(body["random_seed"]?.Value<int>(), Is.EqualTo(7));
        Assert.That(body.ContainsKey("seed"), Is.False);
    }

    [Test]
    public void VendorExtensionsWithoutRandomSeed_KeepSeed()
    {
        ChatRequest request = CreateRequest();
        request.Seed = 42;
        request.VendorExtensions = new ChatRequestVendorExtensions
        {
            Mistral = new ChatRequestVendorMistralExtensions { SafePrompt = true }
        };

        JObject body = Serialize(request);

        Assert.That(body["random_seed"]?.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void NoSeed_SendsNeitherField()
    {
        JObject body = Serialize(CreateRequest());

        Assert.That(body.ContainsKey("random_seed"), Is.False);
        Assert.That(body.ContainsKey("seed"), Is.False);
    }
}
