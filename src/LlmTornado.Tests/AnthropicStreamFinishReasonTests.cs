using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicStreamFinishReasonTests
{
    private const string TextSse = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_text","type":"message","role":"assistant","content":[],"model":"claude-sonnet-4-6","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":40,"output_tokens":1}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Paris is cloudy."}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":6}}

        event: message_stop
        data: {"type":"message_stop"}

        """;

    private const string ToolUseSse = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_tool","type":"message","role":"assistant","content":[],"model":"claude-sonnet-4-6","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":40,"output_tokens":1}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_paris","name":"get_weather","input":{}}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"city\": \"Paris\"}"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":30}}

        event: message_stop
        data: {"type":"message_stop"}

        """;

    [Test]
    public async Task ChatClient_Streaming_TextReply_ReportsStopFinishReason()
    {
        Assert.That(await StreamFinishReason(TextSse), Is.EqualTo(ChatFinishReason.Stop));
    }

    [Test]
    public async Task ChatClient_Streaming_ToolUseReply_ReportsToolCallsFinishReason()
    {
        Assert.That(await StreamFinishReason(ToolUseSse), Is.EqualTo(ChatFinishReason.ToolCalls));
    }

    private static async Task<ChatFinishReason?> StreamFinishReason(string sse)
    {
        using FakeSseServer server = new FakeSseServer(sse);
        TornadoApi api = new TornadoApi(new AnthropicEndpointProvider
        {
            Auth = new ProviderAuthentication(LLmProviders.Anthropic, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });
        IChatClient client = api.AsChatClient(ChatModel.Anthropic.Claude46.Sonnet);

        ChatFinishReason? finishReason = null;
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(
                           [new ChatMessage(ChatRole.User, "What is the weather in Paris?")]))
        {
            finishReason = update.FinishReason ?? finishReason;
        }

        return finishReason;
    }

    private sealed class FakeSseServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string body;

        public int Port { get; }

        public FakeSseServer(string body)
        {
            this.body = body;

            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            Port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            listener.Start();
            _ = Serve();
        }

        private async Task Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // listener closed
                    return;
                }

                using (StreamReader reader = new StreamReader(context.Request.InputStream))
                {
                    await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/event-stream";
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            listener.Close();
        }
    }
}
