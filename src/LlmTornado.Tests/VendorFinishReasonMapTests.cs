using LlmTornado.Chat;
using LlmTornado.Code;

namespace LlmTornado.Tests;

[TestFixture]
public class VendorFinishReasonMapTests
{
    [TestCase("model_length", ChatMessageFinishReasons.Length)]
    [TestCase("sensitive", ChatMessageFinishReasons.ContentFilter)]
    [TestCase("network_error", ChatMessageFinishReasons.Error)]
    [TestCase("insufficient_system_resource", ChatMessageFinishReasons.Error)]
    [TestCase("aborted", ChatMessageFinishReasons.Cancel)]
    public void Map_ResolvesVendorSpecificFinishReasons(string reason, ChatMessageFinishReasons expected)
    {
        Assert.That(ChatMessageFinishReasonsConverter.Map.GetValueOrDefault(reason, ChatMessageFinishReasons.Unknown), Is.EqualTo(expected));
    }
}
