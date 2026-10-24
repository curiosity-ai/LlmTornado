using LlmTornado.Chat;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class ChatCacheWriteUsageTests
{
    [Test]
    public void Usage_MapsPromptTokenDetailsCacheWriteTokensToCacheCreationTokens()
    {
        const string json = """
            {"prompt_tokens":100,"completion_tokens":20,"total_tokens":120,"prompt_tokens_details":{"cached_tokens":20,"cache_write_tokens":30}}
            """;

        ChatUsage? usage = JsonConvert.DeserializeObject<ChatUsage>(json);

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.PromptTokenDetails?.CacheWriteTokens, Is.EqualTo(30));
        Assert.That(usage.CacheCreationTokens, Is.EqualTo(30));
    }

    [Test]
    public void Usage_WithoutCacheWriteTokensHasNoCacheCreationTokens()
    {
        ChatUsage? usage = JsonConvert.DeserializeObject<ChatUsage>("""{"prompt_tokens":100,"completion_tokens":20,"total_tokens":120}""");

        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.CacheCreationTokens, Is.Null);
    }
}
