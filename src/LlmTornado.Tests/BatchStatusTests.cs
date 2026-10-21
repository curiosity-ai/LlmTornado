using LlmTornado.Batch;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class BatchStatusTests
{
    [Test]
    public void Deserialize_AnthropicBatchThatIsCanceling()
    {
        // Anthropic spells the in-flight cancellation state "canceling" (one l), unlike OpenAI's "cancelling".
        const string json = """
            {"id":"msgbatch_01","type":"message_batch","processing_status":"canceling","request_counts":{"processing":2,"succeeded":0,"errored":0,"canceled":0,"expired":0}}
            """;

        BatchItem? batch = JsonConvert.DeserializeObject<BatchItem>(json);

        Assert.That(batch, Is.Not.Null);
        Assert.That(batch!.Status, Is.EqualTo(BatchStatus.Canceling));
    }

    [Test]
    public void Deserialize_OpenAiBatchThatIsCancelling()
    {
        const string json = """
            {"id":"batch_01","object":"batch","status":"cancelling"}
            """;

        BatchItem? batch = JsonConvert.DeserializeObject<BatchItem>(json);

        Assert.That(batch!.Status, Is.EqualTo(BatchStatus.Cancelling));
    }
}
