using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Tests;

[TestFixture]
public class UnknownModelProviderTests
{
    [Test]
    public void GetProvider_ModelOfUnknownProvider_UsesTheAuthenticatedProvider()
    {
        // A mistyped model name resolves to no provider. With a Google key only, the request must
        // go to Google, not to an OpenAI endpoint that rejects the key as "incorrect".
        TornadoApi api = new TornadoApi(LLmProviders.Google, "test-key");
        ChatModel model = "gemini-3-image-pro-preview-typo";

        Assert.That(model.Provider, Is.EqualTo(LLmProviders.Unknown));
        Assert.That(api.GetProvider(model).Provider, Is.EqualTo(LLmProviders.Google));
    }

    [Test]
    public void GetProvider_ModelOfUnknownProvider_FallsBackToOpenAiWithoutAnyKey()
    {
        TornadoApi api = new TornadoApi();
        ChatModel model = "no-such-model";

        Assert.That(api.GetProvider(model).Provider, Is.EqualTo(LLmProviders.OpenAi));
    }
}
