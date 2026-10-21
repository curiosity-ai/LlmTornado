using LlmTornado.Files;
using LlmTornado.Files.Vendors.Anthropic;
using LlmTornado.Files.Vendors.Google;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class FileVendorMappingTests
{
    [Test]
    public void Anthropic_ToFile_KeepsTheFileName()
    {
        VendorAnthropicTornadoFile? vendor = JsonConvert.DeserializeObject<VendorAnthropicTornadoFile>("""
            { "id": "file_011", "type": "file", "filename": "report.pdf", "mime_type": "application/pdf", "size_bytes": 1024, "created_at": "2025-01-02T03:04:05Z", "downloadable": false }
            """);

        TornadoFile file = vendor!.ToFile();

        Assert.That(file.Name, Is.EqualTo("report.pdf"));
    }

    [Test]
    public void Google_ToFile_KeepsDisplayNameSizeAndCreationTime()
    {
        VendorGoogleTornadoFileContent? vendor = JsonConvert.DeserializeObject<VendorGoogleTornadoFileContent>("""
            { "name": "files/abc", "displayName": "report.pdf", "mimeType": "application/pdf", "sizeBytes": "1024", "createTime": "2025-01-02T03:04:05Z", "uri": "https://example.com/files/abc", "state": "ACTIVE" }
            """);

        TornadoFile file = vendor!.ToFile(null);

        Assert.Multiple(() =>
        {
            Assert.That(file.Name, Is.EqualTo("report.pdf"));
            Assert.That(file.Bytes, Is.EqualTo(1024));
            Assert.That(file.CreatedAt, Is.EqualTo(new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero).ToUnixTimeSeconds()));
        });
    }

    [Test]
    public void Google_ToFile_WithoutCreateTime_LeavesCreatedAtZero()
    {
        VendorGoogleTornadoFileContent? vendor = JsonConvert.DeserializeObject<VendorGoogleTornadoFileContent>("""
            { "name": "files/abc", "mimeType": "text/plain", "state": "ACTIVE" }
            """);

        Assert.That(vendor!.ToFile(null).CreatedAt, Is.EqualTo(0));
    }
}
