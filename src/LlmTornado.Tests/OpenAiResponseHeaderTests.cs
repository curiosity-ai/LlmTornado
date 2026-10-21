using System.Net.Http;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class OpenAiResponseHeaderTests
{
    private static ChatResult Parse(Action<HttpResponseMessage> headers)
    {
        using HttpResponseMessage response = new HttpResponseMessage();
        headers(response);
        ChatResult result = new ChatResult();
        new OpenAiEndpointProvider(LLmProviders.OpenAi).ParseInboundHeaders(result, response);
        return result;
    }

    [Test]
    public void ParseInboundHeaders_RequestIdAndVersionAreSeparate()
    {
        ChatResult result = Parse(r =>
        {
            r.Headers.TryAddWithoutValidation("x-request-id", "req_abc123");
            r.Headers.TryAddWithoutValidation("Openai-Version", "2020-10-01");
        });

        Assert.That(result.RequestId, Is.EqualTo("req_abc123"));
        Assert.That(result.Version, Is.EqualTo("2020-10-01"));
    }

    [Test]
    public void ParseInboundHeaders_VersionAloneDoesNotBecomeRequestId()
    {
        ChatResult result = Parse(r => r.Headers.TryAddWithoutValidation("Openai-Version", "2020-10-01"));

        Assert.That(result.RequestId, Is.Null);
        Assert.That(result.Version, Is.EqualTo("2020-10-01"));
    }

    [Test]
    public void ParseInboundHeaders_LegacyRequestIdHeaderStillWorks()
    {
        ChatResult result = Parse(r => r.Headers.TryAddWithoutValidation("X-RequestSettings-ID", "legacy-1"));

        Assert.That(result.RequestId, Is.EqualTo("legacy-1"));
    }
}
