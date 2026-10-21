using LlmTornado.Audio.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Audio;
using LlmTornado.Code;
using System.IO;
using System.Text;
using LlmTornado.Audio.Vendors.Mistral;

namespace LlmTornado.Tests;

[TestFixture]
public class MistralSpeechResultTests
{
    [Test]
    public async Task ToAudioStream_DecodesTheAudioDataOfTheJsonEnvelope()
    {
        byte[] audio = [0x49, 0x44, 0x33, 0x04, 0x00, 0xFF];
        string json = $"{{\"audio_data\":\"{System.Convert.ToBase64String(audio)}\"}}";

        Stream result = await VendorMistralSpeechResult.ToAudioStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        Assert.That(ReadAll(result), Is.EqualTo(audio));
    }

    [Test]
    public async Task ToAudioStream_LeavesRawAudioUntouched()
    {
        byte[] audio = [0x49, 0x44, 0x33, 0x04, 0x00, 0xFF];

        Stream result = await VendorMistralSpeechResult.ToAudioStream(new MemoryStream(audio));

        Assert.That(ReadAll(result), Is.EqualTo(audio));
    }

    private static byte[] ReadAll(Stream stream)
    {
        MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}

[TestFixture]
[NonParallelizable]
public class MistralSpeechEndpointTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] audio = [0x49, 0x44, 0x33, 0x04, 0x00, 0xFF];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"audio_data\":\"{System.Convert.ToBase64String(audio)}\"}}", Encoding.UTF8, "application/json")
            });
        }
    }

    [Test]
    public async Task CreateSpeech_ReturnsTheDecodedAudioOfAMistralResponse()
    {
        Func<LLmProviders, HttpClient?>? previous = EndpointBase.OnHttpClientRequested;
        EndpointBase.OnHttpClientRequested = provider => provider is LLmProviders.Mistral ? new HttpClient(new StubHandler()) : null;

        try
        {
            TornadoApi api = new TornadoApi(new Uri("http://mistral.test/v1"), "key", LLmProviders.Mistral);
            SpeechTtsResult? result = await api.Audio.CreateSpeech(new SpeechRequest
            {
                Model = AudioModel.Mistral.Free.VoxtralMiniTts2603,
                Input = "hello",
                VoiceId = "voice"
            });

            Assert.That(result, Is.Not.Null);
            MemoryStream copy = new MemoryStream();
            await result!.AudioStream.CopyToAsync(copy);
            Assert.That(copy.ToArray(), Is.EqualTo(new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0xFF }));
        }
        finally
        {
            EndpointBase.OnHttpClientRequested = previous;
        }
    }
}
