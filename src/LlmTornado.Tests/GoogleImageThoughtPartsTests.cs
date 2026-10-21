using LlmTornado.Images;
using LlmTornado.Images.Vendors.Google;
using Newtonsoft.Json;

namespace LlmTornado.Tests;

[TestFixture]
public class GoogleImageThoughtPartsTests
{
    [Test]
    public void ToChatResult_SkipsThoughtImages()
    {
        VendorGoogleImageResult? result = JsonConvert.DeserializeObject<VendorGoogleImageResult>("""
            {
              "candidates": [
                {
                  "content": {
                    "role": "model",
                    "parts": [
                      { "thought": true, "inlineData": { "mimeType": "image/png", "data": "dGhvdWdodA==" } },
                      { "inlineData": { "mimeType": "image/png", "data": "ZmluYWw=" } }
                    ]
                  }
                }
              ]
            }
            """);

        ImageGenerationResult images = result!.ToChatResult(null);

        Assert.That(images.Data!.Select(x => x.Base64), Is.EqualTo(new[] { "ZmluYWw=" }));
    }

    [Test]
    public void ToChatResult_KeepsAllImagesWithoutThoughts()
    {
        VendorGoogleImageResult? result = JsonConvert.DeserializeObject<VendorGoogleImageResult>("""
            {
              "candidates": [
                {
                  "content": {
                    "role": "model",
                    "parts": [
                      { "inlineData": { "mimeType": "image/png", "data": "YQ==" } },
                      { "inlineData": { "mimeType": "image/png", "data": "Yg==" } }
                    ]
                  }
                }
              ]
            }
            """);

        ImageGenerationResult images = result!.ToChatResult(null);

        Assert.That(images.Data!.Select(x => x.Base64), Is.EqualTo(new[] { "YQ==", "Yg==" }));
    }
}
