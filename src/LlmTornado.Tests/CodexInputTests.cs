using LlmTornado.Codex;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class CodexInputTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void EmptyPartsAreRejected(string? value)
    {
        Assert.Throws<ArgumentException>(() => CodexInput.Text(value!));
        Assert.Throws<ArgumentException>(() => CodexInput.Image(value!));
        Assert.Throws<ArgumentException>(() => CodexInput.LocalImage(value!));
    }

    [TestCase("relative.png")]
    [TestCase("file:///tmp/image.png")]
    [TestCase("ftp://example.com/image.png")]
    [TestCase("data:text/plain;base64,aGVsbG8=")]
    [TestCase("data:image/png,hello")]
    [TestCase("data:image/png;base64,")]
    public void InvalidImageUrlsAreRejected(string url)
        => Assert.Throws<ArgumentException>(() => CodexInput.Image(url));

    [Test]
    public async Task LargeDataUrlsAreAcceptedWithoutUriLengthRestrictions()
    {
        string url = "data:image/png;base64," + Convert.ToBase64String(new byte[100_000]);
        CodexOAuthHistoryItem item = await CodexOAuthHistoryItem.UserMessageAsync(new[] { CodexInput.Image(url) });

        Assert.That(item.ToJson()["content"]?[0]?["image_url"]?.Value<string>(), Is.EqualTo(url));
    }

    [Test]
    public void RelativeLocalImagePathsAreResolvedAtCreation()
    {
        const string path = "screenshot.png";
        string expected = Path.GetFullPath(path);
        CodexInput input = CodexInput.LocalImage(path);

        Assert.That(CodexInput.ToAppServerInput(new[] { input })[0]?["path"]?.Value<string>(), Is.EqualTo(expected));
    }

    [Test]
    public async Task ImageRead_CancellationDuringReadDoesNotReturnPartialData()
    {
        using PendingReadStream stream = new PendingReadStream();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<string> reading = CodexInput.ReadImageDataUrlAsync(stream, "image/png", cancellation.Token);
        await stream.PendingReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(stream.BytesRead, Is.EqualTo(3));
        Assert.That(reading.IsCompleted, Is.False, "The asynchronous read must return control before its source completes.");

        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await reading.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(reading.IsCanceled, Is.True);
        Assert.That(stream.CanRead, Is.True, "The caller retains ownership of the source stream.");

        stream.Dispose();
        Assert.That(stream.CanRead, Is.False, "The test stream must report when its owner disposes it.");
    }

    [Test]
    public async Task ImageRead_ReturnsCompleteDataAndLeavesSourceOpen()
    {
        byte[] bytes = [1, 2, 3, 4, 5];
        using MemoryStream stream = new MemoryStream(bytes);

        string result = await CodexInput.ReadImageDataUrlAsync(stream, "image/png", CancellationToken.None);

        Assert.That(result, Is.EqualTo("data:image/png;base64," + Convert.ToBase64String(bytes)));
        Assert.That(stream.CanRead, Is.True);
    }

    private sealed class PendingReadStream : Stream
    {
        private bool disposed;

        internal TaskCompletionSource<bool> PendingReadStarted { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int BytesRead { get; private set; }
        public override bool CanRead => !disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BytesRead == 0)
            {
                buffer[offset] = 1;
                buffer[offset + 1] = 2;
                buffer[offset + 2] = 3;
                BytesRead = 3;
                return 3;
            }

            TaskCompletionSource<int> pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken)))
            {
                PendingReadStarted.TrySetResult(true);
                return await pending.Task.ConfigureAwait(false);
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous reads are not supported.");
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            disposed = true;
            base.Dispose(disposing);
        }
    }
}
