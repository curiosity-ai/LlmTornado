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
public class ResponsesStreamEventNameTests
{
    private const string Stream =
        "event: response.code_interpreter_call.in_progress\n" +
        "data: {\"type\":\"response.code_interpreter_call.in_progress\",\"item_id\":\"ci_1\",\"output_index\":0,\"sequence_number\":1}\n\n" +
        "event: response.code_interpreter_call.interpreting\n" +
        "data: {\"type\":\"response.code_interpreter_call.interpreting\",\"item_id\":\"ci_1\",\"output_index\":0,\"sequence_number\":2}\n\n" +
        "event: response.code_interpreter_call.completed\n" +
        "data: {\"type\":\"response.code_interpreter_call.completed\",\"item_id\":\"ci_1\",\"output_index\":0,\"sequence_number\":3}\n\n" +
        "event: response.output_text.annotation.added\n" +
        "data: {\"type\":\"response.output_text.annotation.added\",\"item_id\":\"msg_1\",\"output_index\":1,\"content_index\":0,\"annotation_index\":0,\"sequence_number\":4,\"annotation\":{\"type\":\"url_citation\",\"url\":\"https://example.com\",\"title\":\"Example\",\"start_index\":0,\"end_index\":5}}\n\n";

    [Test]
    public async Task Stream_CodeInterpreterAndAnnotationEvents_ReachTheirHandlers()
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
            OnResponseCodeInterpreterCallInProgress = e => { seen.Add("in_progress:" + e.ItemId); return ValueTask.CompletedTask; },
            OnResponseCodeInterpreterCallInterpreting = e => { seen.Add("interpreting:" + e.ItemId); return ValueTask.CompletedTask; },
            OnResponseCodeInterpreterCallCompleted = e => { seen.Add("completed:" + e.ItemId); return ValueTask.CompletedTask; },
            OnResponseOutputTextAnnotationAdded = e => { seen.Add("annotation:" + e.ItemId); return ValueTask.CompletedTask; }
        });

        Assert.That(seen, Is.EqualTo(new[] { "in_progress:ci_1", "interpreting:ci_1", "completed:ci_1", "annotation:msg_1" }));
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
