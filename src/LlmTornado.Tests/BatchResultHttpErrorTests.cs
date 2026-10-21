using LlmTornado.Batch;
using LlmTornado.Code;

namespace LlmTornado.Tests;

[TestFixture]
public class BatchResultHttpErrorTests
{
    [Test]
    public void OpenAi_LineWithHttpErrorStatus_IsAnErroredResult()
    {
        // OpenAI writes a request that reached the model but was rejected to the output file with its HTTP status
        const string line = """
            {"id":"batch_req_1","custom_id":"req-1","response":{"status_code":400,"request_id":"r1","body":{"error":{"message":"Invalid value","type":"invalid_request_error","code":"invalid_value"}}},"error":null}
            """;

        BatchResult? result = BatchResult.Deserialize(LLmProviders.OpenAi, line);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(400));
        Assert.That(result.ResultType, Is.EqualTo(BatchResultType.Errored));
    }

    [Test]
    public void OpenAi_LineWithOkStatus_IsSucceeded()
    {
        const string line = """
            {"id":"batch_req_1","custom_id":"req-1","response":{"status_code":200,"request_id":"r1","body":{"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"gpt-4o","choices":[{"index":0,"message":{"role":"assistant","content":"hi"},"finish_reason":"stop"}]}},"error":null}
            """;

        BatchResult? result = BatchResult.Deserialize(LLmProviders.OpenAi, line);

        Assert.That(result!.ResultType, Is.EqualTo(BatchResultType.Succeeded));
    }
}
