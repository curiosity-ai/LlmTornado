using LlmTornado.Chat;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class DeepSeekCacheUsageTests
{
    [Test]
    public void Usage_MapsPromptCacheHitTokensToCacheReadTokens()
    {
        const string json = """
            {"prompt_tokens":100,"completion_tokens":20,"total_tokens":120,"prompt_cache_hit_tokens":64,"prompt_cache_miss_tokens":36}
            """;

        ChatUsage? usage = JsonConvert.DeserializeObject<ChatUsage>(json);

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.CacheReadTokens, Is.EqualTo(64));
    }

    [Test]
    public void Usage_KeepsThePromptTokenDetailsWhenTheyReportCachedTokens()
    {
        const string json = """
            {"prompt_tokens":100,"completion_tokens":20,"total_tokens":120,"prompt_tokens_details":{"cached_tokens":50},"prompt_cache_hit_tokens":64}
            """;

        ChatUsage? usage = JsonConvert.DeserializeObject<ChatUsage>(json);

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.PromptTokenDetails?.CachedTokens, Is.EqualTo(50));
        Assert.That(usage.CacheReadTokens, Is.Null);
    }

    [Test]
    public void Usage_WithoutCacheFieldsHasNoCacheReadTokens()
    {
        ChatUsage? usage = JsonConvert.DeserializeObject<ChatUsage>("""{"prompt_tokens":100,"completion_tokens":20,"total_tokens":120}""");

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.CacheReadTokens, Is.Null);
    }
}
