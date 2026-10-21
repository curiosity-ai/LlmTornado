using LlmTornado.Embedding;
using LlmTornado.Embedding.Vendors.Cohere;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class CohereEmbeddingUsageTests
{
    [Test]
    public void ToResult_WithoutMeta_DoesNotThrow()
    {
        VendorCohereEmbeddingResult? vendor = JsonConvert.DeserializeObject<VendorCohereEmbeddingResult>("""
            { "id": "e1", "embeddings": { "float": [[0.1, 0.2]] } }
            """);

        EmbeddingResult result = null!;
        Assert.DoesNotThrow(() => result = vendor!.ToResult(null));
        Assert.That(result.Data, Has.Count.EqualTo(1));
    }

    [Test]
    public void ToResult_MetaWithoutBilledUnits_DoesNotThrow()
    {
        VendorCohereEmbeddingResult? vendor = JsonConvert.DeserializeObject<VendorCohereEmbeddingResult>("""
            { "id": "e1", "embeddings": { "float": [[0.1, 0.2]] }, "meta": { "api_version": { "version": 2 } } }
            """);

        Assert.DoesNotThrow(() => vendor!.ToResult(null));
    }

    [Test]
    public void ToResult_ReportsBilledInputTokens()
    {
        VendorCohereEmbeddingResult? vendor = JsonConvert.DeserializeObject<VendorCohereEmbeddingResult>("""
            { "id": "e1", "embeddings": { "float": [[0.1, 0.2]] }, "meta": { "billed_units": { "input_tokens": 7 } } }
            """);

        EmbeddingResult result = vendor!.ToResult(null);

        Assert.That(result.Usage.PromptTokens, Is.EqualTo(7));
    }
}
