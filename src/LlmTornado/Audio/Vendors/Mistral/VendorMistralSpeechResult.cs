using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Audio.Vendors.Mistral;

/// <summary>
/// Mistral answers a non-streaming speech request with a JSON object whose <c>audio_data</c> holds the audio base64 encoded.
/// </summary>
internal static class VendorMistralSpeechResult
{
    /// <summary>
    /// Replaces the JSON envelope of a Mistral speech response with the decoded audio. A body that is not that envelope is returned unchanged.
    /// </summary>
    public static async Task<Stream> ToAudioStream(Stream body)
    {
        using (body)
        {
            MemoryStream buffer = new MemoryStream();
            await body.CopyToAsync(buffer).ConfigureAwait(false);
            byte[] bytes = buffer.ToArray();

            if (bytes.Length > 0 && bytes[0] is (byte)'{')
            {
                try
                {
                    string? audioData = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes))["audio_data"]?.Value<string>();

                    if (audioData is not null)
                    {
                        return new MemoryStream(Convert.FromBase64String(audioData));
                    }
                }
                catch (Exception e) when (e is FormatException or Newtonsoft.Json.JsonException)
                {
                    // not the envelope, hand the bytes back as they are
                }
            }

            return new MemoryStream(bytes);
        }
    }
}
