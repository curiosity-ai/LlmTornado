using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

#pragma warning disable MEAI001

namespace LlmTornado.Tests;

[TestFixture]
public class MeaiImageMediaTypeTests
{
    private const string OpenAiReply = """{"created":1,"data":[{"b64_json":"AAAA"}]}""";

    private const string GoogleReply = """
        {"candidates":[{"content":{"role":"model","parts":[{"inlineData":{"mimeType":"image/jpeg","data":"AAAA"}}]},"finishReason":"STOP"}]}
        """;

    [Test]
    public async Task OpenAi_MediaType_IsSentAsOutputFormatAndUsedForTheContent()
    {
        using FakeServer server = new FakeServer(OpenAiReply);
        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });
        IImageGenerator generator = new TornadoImageGenerator(api, "gpt-image-1");

        ImageGenerationResponse response = await generator.GenerateAsync(
            new ImageGenerationRequest("A red fox"),
            new ImageGenerationOptions { MediaType = "image/jpeg" });

        Assert.That(JObject.Parse(server.RequestBodies.Single())["output_format"]?.Value<string>(), Is.EqualTo("jpeg"));
        Assert.That(response.Contents.OfType<DataContent>().Single().MediaType, Is.EqualTo("image/jpeg"));
    }

    [Test]
    public async Task OpenAi_NoMediaType_SendsNoOutputFormatAndDefaultsToPng()
    {
        using FakeServer server = new FakeServer(OpenAiReply);
        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });
        IImageGenerator generator = new TornadoImageGenerator(api, "gpt-image-1");

        ImageGenerationResponse response = await generator.GenerateAsync(new ImageGenerationRequest("A red fox"));

        Assert.That(JObject.Parse(server.RequestBodies.Single()).ContainsKey("output_format"), Is.False);
        Assert.That(response.Contents.OfType<DataContent>().Single().MediaType, Is.EqualTo("image/png"));
    }

    [Test]
    public async Task Google_ReportedMimeType_IsUsedForTheContent()
    {
        using FakeServer server = new FakeServer(GoogleReply);
        TornadoApi api = new TornadoApi(LLmProviders.Google, "test-key");
        ((GoogleEndpointProvider)api.GetProvider(LLmProviders.Google)).UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1beta/{{0}}{{1}}";
        IImageGenerator generator = new TornadoImageGenerator(api, "gemini-3.1-flash-image");

        ImageGenerationResponse response = await generator.GenerateAsync(new ImageGenerationRequest("A red fox"));

        Assert.That(response.Contents.OfType<DataContent>().Single().MediaType, Is.EqualTo("image/jpeg"));
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
                context.Response.ContentType = "application/json";
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
