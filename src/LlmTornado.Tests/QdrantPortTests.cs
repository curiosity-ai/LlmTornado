using System.Net;
using System.Net.Sockets;
using LlmTornado.VectorDatabases.Qdrant;

namespace LlmTornado.Tests;

[TestFixture]
public class QdrantPortTests
{
    [Test]
    public async Task Constructor_ConnectsToTheGivenPort()
    {
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        QdrantVectorDatabase database = new QdrantVectorDatabase("127.0.0.1", port);

        Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await database.GetCollectionList();
            }
            catch
            {
                // The listener is not a Qdrant server, only the connection attempt matters.
            }
        });

        Task winner = await Task.WhenAny(accepted, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.That(winner, Is.SameAs(accepted), "no connection reached the requested port");

        (await accepted).Dispose();
    }
}
