using LlmTornado.Chat;
using LlmTornado.Vendor.Google;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleChatUsageTests
{
    [Test]
    public void CandidatesCountPlusThoughtsIsTheCompletion()
    {
        ChatUsage usage = new ChatUsage(new VendorGoogleUsage
        {
            PromptTokenCount = 100,
            CandidatesTokenCount = 5,
            ThoughtsTokenCount = 30,
            TotalTokenCount = 135
        });

        Assert.That(usage.PromptTokens, Is.EqualTo(100));
        Assert.That(usage.CompletionTokens, Is.EqualTo(35));
        Assert.That(usage.CompletionTokensDetails?.ReasoningTokens, Is.EqualTo(30));
        Assert.That(usage.TotalTokens, Is.EqualTo(usage.PromptTokens + usage.CompletionTokens));
    }

    [Test]
    public void AbsentCandidatesCountDoesNotReportThePromptSizeAsOutput()
    {
        // Gemini leaves candidatesTokenCount out when thinking used the whole output budget.
        VendorGoogleUsage? vendorUsage = Newtonsoft.Json.JsonConvert.DeserializeObject<VendorGoogleUsage>(
            """{"promptTokenCount":100,"cachedContentTokenCount":40,"thoughtsTokenCount":50,"totalTokenCount":150}""");

        ChatUsage usage = new ChatUsage(vendorUsage!);

        Assert.That(usage.CompletionTokens, Is.EqualTo(50));
        Assert.That(usage.PromptTokens, Is.EqualTo(100));
        Assert.That(usage.CacheReadTokens, Is.EqualTo(40));
    }

    [Test]
    public void AnEmptyReplyWithoutThoughtsHasNoCompletionTokens()
    {
        VendorGoogleUsage? vendorUsage = Newtonsoft.Json.JsonConvert.DeserializeObject<VendorGoogleUsage>(
            """{"promptTokenCount":100,"totalTokenCount":100}""");

        ChatUsage usage = new ChatUsage(vendorUsage!);

        Assert.That(usage.CompletionTokens, Is.EqualTo(0));
    }

    [Test]
    public void ToolUsePromptTokensCountAsPrompt()
    {
        ChatUsage usage = new ChatUsage(new VendorGoogleUsage
        {
            PromptTokenCount = 100,
            ToolUsePromptTokenCount = 40,
            CandidatesTokenCount = 5,
            TotalTokenCount = 145
        });

        Assert.That(usage.PromptTokens, Is.EqualTo(140));
        Assert.That(usage.CompletionTokens, Is.EqualTo(5));
        Assert.That(usage.CompletionTokensDetails?.ToolsUseTokens, Is.EqualTo(40));
        Assert.That(usage.TotalTokens, Is.EqualTo(usage.PromptTokens + usage.CompletionTokens));
    }
}
