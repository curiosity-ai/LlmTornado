using LlmTornado.Responses;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class ResponseUsageTests
{
    [Test]
    public void Deserialize_ReadsTheCachedAndReasoningTokenDetailsOfTheResponsesApi()
    {
        const string json = """
            {"input_tokens":1200,"input_tokens_details":{"cached_tokens":1024},"output_tokens":300,"output_tokens_details":{"reasoning_tokens":192},"total_tokens":1500}
            """;

        ResponseUsage? usage = JsonConvert.DeserializeObject<ResponseUsage>(json);

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.InputTokenDetails?.CachedTokens, Is.EqualTo(1024));
        Assert.That(usage.OutputTokenDetails?.ReasoningTokens, Is.EqualTo(192));
    }
}
