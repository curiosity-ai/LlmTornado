using LlmTornado.Code;
using LlmTornado.Embedding;
using LlmTornado.Embedding.Models;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class CohereEmbeddingDimensionsTests
{
    private static JObject Serialize(EmbeddingRequest request)
    {
        TornadoRequestContent serialized = request.Serialize(new TornadoApi("test-key").GetProvider(LLmProviders.Cohere));
        return JObject.Parse(serialized.Body.ToString()!);
    }

    [Test]
    public void Dimensions_AreSentAsOutputDimension()
    {
        EmbeddingRequest request = new EmbeddingRequest(EmbeddingModel.Cohere.Gen4.V4, "hello", 512);

        Assert.That(Serialize(request)["output_dimension"]?.Value<int>(), Is.EqualTo(512));
    }

    [Test]
    public void OutputDimension_IsOmitted_WhenDimensionsAreNotSet()
    {
        EmbeddingRequest request = new EmbeddingRequest(EmbeddingModel.Cohere.Gen4.V4, "hello");

        Assert.That(Serialize(request).ContainsKey("output_dimension"), Is.False);
    }
}
