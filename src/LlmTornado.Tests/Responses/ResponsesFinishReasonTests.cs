using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Responses;
using Newtonsoft.Json;

namespace LlmTornado.Tests.Responses;

[TestFixture]
public class ResponsesFinishReasonTests
{
    private static ChatChoice Convert(string json)
    {
        ResponseResult result = JsonConvert.DeserializeObject<ResponseResult>(json)!;
        return ResponseHelpers.ToChatChoice(result, new ResponseRequest(), new OpenAiEndpointProvider());
    }

    [Test]
    public void ToChatChoice_CompletedMessage_ReportsEndTurn()
    {
        ChatChoice choice = Convert("""
            {"id":"resp_1","status":"completed","output":[{"type":"message","id":"msg_1","role":"assistant","status":"completed","content":[{"type":"output_text","text":"Paris."}]}]}
            """);

        Assert.That(choice.FinishReason, Is.EqualTo(ChatMessageFinishReasons.EndTurn));
    }

    [Test]
    public void ToChatChoice_FunctionCall_ReportsToolCalls()
    {
        ChatChoice choice = Convert("""
            {"id":"resp_1","status":"completed","output":[{"type":"function_call","id":"fc_1","call_id":"call_1","name":"get_weather","arguments":"{}","status":"completed"}]}
            """);

        Assert.That(choice.FinishReason, Is.EqualTo(ChatMessageFinishReasons.ToolCalls));
    }

    [TestCase("max_output_tokens", ChatMessageFinishReasons.Length)]
    [TestCase("content_filter", ChatMessageFinishReasons.ContentFilter)]
    public void ToChatChoice_IncompleteResponse_MapsTheIncompleteReason(string reason, ChatMessageFinishReasons expected)
    {
        ChatChoice choice = Convert($$"""
            {"id":"resp_1","status":"incomplete","incomplete_details":{"reason":"{{reason}}"},"output":[{"type":"message","id":"msg_1","role":"assistant","status":"incomplete","content":[{"type":"output_text","text":"Par"}]}]}
            """);

        Assert.That(choice.FinishReason, Is.EqualTo(expected));
    }

    [Test]
    public void ToChatChoice_FailedResponse_ReportsError()
    {
        ChatChoice choice = Convert("""
            {"id":"resp_1","status":"failed","error":{"code":"server_error","message":"boom"},"output":[]}
            """);

        Assert.That(choice.FinishReason, Is.EqualTo(ChatMessageFinishReasons.Error));
    }
}
