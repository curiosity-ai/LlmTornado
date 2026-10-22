using System.Net;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Files;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleFilesListTests
{
    private const string FilesJson = """
        { "files": [ { "name": "files/abc", "mimeType": "text/plain", "uri": "https://example.com/files/abc", "state": "ACTIVE" } ], "nextPageToken": "next" }
        """;

    [Test]
    public async Task Get_WithoutProvider_ListsGoogleFilesWhenGoogleIsTheAuthenticatedProvider()
    {
        TornadoPagingList<TornadoFile>? files = await ListFiles(api => api.Files.Get());

        Assert.That(files, Is.Not.Null);
        Assert.That(files!.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Get_WithGoogleProvider_ReturnsTheNextPageToken()
    {
        TornadoPagingList<TornadoFile>? files = await ListFiles(api => api.Files.Get(provider: LLmProviders.Google));

        Assert.That(files?.PageToken, Is.EqualTo("next"));
    }

    private static async Task<TornadoPagingList<TornadoFile>?> ListFiles(Func<TornadoApi, Task<TornadoPagingList<TornadoFile>?>> list)
    {
        Func<IEndpointProvider, Task<HttpClient?>>? previous = TornadoConfig.CreateClientAsync;
        TornadoConfig.CreateClientAsync = _ => Task.FromResult<HttpClient?>(new HttpClient(new StubHandler(FilesJson)));

        try
        {
            return await list(new TornadoApi(LLmProviders.Google, "key"));
        }
        finally
        {
            TornadoConfig.CreateClientAsync = previous;
        }
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
