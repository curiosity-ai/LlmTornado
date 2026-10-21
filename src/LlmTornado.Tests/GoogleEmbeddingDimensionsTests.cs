using LlmTornado.Code;
using LlmTornado.Embedding;
using LlmTornado.Embedding.Models;
using LlmTornado.Embedding.Vendors.Google;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleEmbeddingDimensionsTests
{
    private static JObject Serialize(EmbeddingRequest request)
    {
        TornadoRequestContent serialized = request.Serialize(new TornadoApi("test-key").GetProvider(LLmProviders.Google));
        return JObject.Parse(serialized.Body.ToString()!);
    }

    [Test]
    public void Dimensions_AreSent_WhenNoGoogleExtensionsAreSet()
    {
        EmbeddingRequest request = new EmbeddingRequest(EmbeddingModel.Google.Gemini.GeminiEmbedding2, "hello", 256);

        Assert.That(Serialize(request)["outputDimensionality"]?.Value<int>(), Is.EqualTo(256));
    }

    [Test]
    public void Dimensions_AreSent_WhenGoogleExtensionsOnlySetATaskType()
    {
        EmbeddingRequest request = new EmbeddingRequest(EmbeddingModel.Google.Gemini.GeminiEmbedding001, "hello", 768, new EmbeddingRequestVendorExtensions(new EmbeddingRequestVendorGoogleExtensions
        {
            TaskType = EmbeddingRequestVendorGoogleExtensionsTaskTypes.RetrievalQuery
        }));

        JObject body = Serialize(request);

        Assert.That(body["taskType"]?.Value<string>(), Is.EqualTo("RETRIEVAL_QUERY"));
        Assert.That(body["outputDimensionality"]?.Value<int>(), Is.EqualTo(768));
    }

    [Test]
    public void ExtensionOutputDimensionality_WinsOverRequestDimensions()
    {
        EmbeddingRequest request = new EmbeddingRequest(EmbeddingModel.Google.Gemini.GeminiEmbedding001, "hello", 768, new EmbeddingRequestVendorExtensions(new EmbeddingRequestVendorGoogleExtensions
        {
            OutputDimensionality = 1536
        }));

        Assert.That(Serialize(request)["outputDimensionality"]?.Value<int>(), Is.EqualTo(1536));
    }
}
