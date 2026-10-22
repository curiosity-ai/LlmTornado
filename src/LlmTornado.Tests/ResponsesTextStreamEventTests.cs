using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Chat.Models;
using LlmTornado.Responses;
using LlmTornado.Responses.Events;

namespace LlmTornado.Tests;

[TestFixture]
public class ResponsesTextStreamEventTests
{
    private const string Stream =
        "event: response.reasoning_text.delta\n" +
        "data: {\"type\":\"response.reasoning_text.delta\",\"item_id\":\"rs_1\",\"output_index\":0,\"content_index\":0,\"delta\":\"Think\",\"sequence_number\":1}\n\n" +
        "event: response.reasoning_text.done\n" +
        "data: {\"type\":\"response.reasoning_text.done\",\"item_id\":\"rs_1\",\"output_index\":0,\"content_index\":0,\"text\":\"Thinking done\",\"sequence_number\":1}\n\n" +
        "event: response.mcp_call_arguments.delta\n" +
        "data: {\"type\":\"response.mcp_call_arguments.delta\",\"item_id\":\"mcp_1\",\"output_index\":1,\"delta\":\"{\\\"q\\\":\",\"sequence_number\":1}\n\n" +
        "event: response.mcp_call_arguments.done\n" +
        "data: {\"type\":\"response.mcp_call_arguments.done\",\"item_id\":\"mcp_1\",\"output_index\":1,\"arguments\":\"{\\\"q\\\":\\\"x\\\"}\",\"sequence_number\":1}\n\n" +
        "event: response.custom_tool_call_input.delta\n" +
        "data: {\"type\":\"response.custom_tool_call_input.delta\",\"item_id\":\"ct_1\",\"output_index\":2,\"delta\":\"ls \",\"sequence_number\":1}\n\n" +
        "event: response.custom_tool_call_input.done\n" +
        "data: {\"type\":\"response.custom_tool_call_input.done\",\"item_id\":\"ct_1\",\"output_index\":2,\"input\":\"ls -la\",\"sequence_number\":1}\n\n";

    [Test]
    public async Task Stream_ReasoningTextMcpArgumentsAndCustomToolInputEvents_ReachTheirHandlers()
    {
        using FakeServer server = new FakeServer(Stream);
        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });

        List<string> seen = [];

        await api.Responses.StreamResponseRich(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt4.O,
            InputString = "Run some code"
        }, new ResponseStreamEventHandler
        {
            OnResponseReasoningTextDelta = e => { seen.Add("reasoning_delta:" + e.Delta); return ValueTask.CompletedTask; },
            OnResponseReasoningDone = e => { seen.Add("reasoning_done:" + e.Text); return ValueTask.CompletedTask; },
            OnResponseMcpCallArgumentsTextDelta = e => { seen.Add("mcp_delta:" + e.Delta); return ValueTask.CompletedTask; },
            OnResponseMcpCallArgumentsTextDone = e => { seen.Add("mcp_done:" + e.Arguments); return ValueTask.CompletedTask; },
            OnResponseCustomToolCallInputDelta = e => { seen.Add("custom_delta:" + e.Delta); return ValueTask.CompletedTask; },
            OnResponseCustomToolCallInputDone = e => { seen.Add("custom_done:" + e.Input); return ValueTask.CompletedTask; }
        });

        Assert.That(seen, Is.EqualTo(new[]
        {
            "reasoning_delta:Think", "reasoning_done:Thinking done",
            "mcp_delta:{\"q\":", "mcp_done:{\"q\":\"x\"}",
            "custom_delta:ls ", "custom_done:ls -la"
        }));
    }

    private sealed class FakeServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string response;

        public int Port { get; }
        public List<string> RequestBodies { get; } = [];

        public FakeServer(string response)
        {
            this.response = response;

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
                    RequestBodies.Add(await reader.ReadToEndAsync().ConfigureAwait(false));
                }

                byte[] bytes = Encoding.UTF8.GetBytes(response);
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
