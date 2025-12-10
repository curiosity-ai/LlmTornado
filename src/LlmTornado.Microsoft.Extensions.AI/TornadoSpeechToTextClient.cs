using System.Runtime.CompilerServices;
using LlmTornado.Audio;
using Microsoft.Extensions.AI;

namespace LlmTornado.Microsoft.Extensions.AI;

/// <summary>
/// Represents a speech to text client for LlmTornado.
/// </summary>
public class TornadoSpeechToTextClient : ISpeechToTextClient
{
    private readonly TornadoApi _api;
    private readonly string _model;

    /// <summary>
    /// Initializes a new instance of the <see cref="TornadoSpeechToTextClient"/> class.
    /// </summary>
    /// <param name="api">The API.</param>
    /// <param name="model">The model.</param>
    public TornadoSpeechToTextClient(TornadoApi api, string? model = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _model = model ?? "whisper-1";
    }

    /// <inheritdoc />
    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream, SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        TranscriptionRequest request = CreateRequest(audioSpeechStream, options, cancellationToken);

        TranscriptionResult? result = await _api.Audio.CreateTranscription(request);
        
        if (result == null)
        {
            throw new InvalidOperationException("Transcription returned null result.");
        }

        return new SpeechToTextResponse(new[] { new TextContent(result.Text) });
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioSpeechStream, SpeechToTextOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        TranscriptionRequest request = CreateRequest(audioSpeechStream, options, cancellationToken);

        // LlmTornado doesn't support streaming transcription yet, so we just await the full result and yield it.
        TranscriptionResult? result = await _api.Audio.CreateTranscription(request);

        if (result == null)
        {
            yield break;
        }

        yield return new SpeechToTextResponseUpdate
        {
             Contents = { new TextContent(result.Text) }
        };
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType == typeof(TornadoApi) ? _api : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // nothing to dispose
    }
    
    private TranscriptionRequest CreateRequest(Stream audioStream, SpeechToTextOptions? options, CancellationToken cancellationToken)
    {
        TranscriptionRequest request = new TranscriptionRequest
        {
            File = new AudioFile(audioStream, AudioFileTypes.Wav),
            Model = _model,
            CancellationToken = cancellationToken
        };

        if (options != null)
        {
            if (!string.IsNullOrEmpty(options.ModelId))
            {
                request.Model = options.ModelId;
            }

            if (!string.IsNullOrEmpty(options.SpeechLanguage))
            {
                request.Language = options.SpeechLanguage;
            }

            if (options.AdditionalProperties != null)
            {
                if (options.AdditionalProperties.TryGetValue("prompt", out object? promptObj) && promptObj is string prompt)
                {
                    request.Prompt = prompt;
                }

                if (options.AdditionalProperties.TryGetValue("temperature", out object? tempObj))
                {
                    if (tempObj is float f)
                    {
                        request.Temperature = f;
                    }
                    else if (tempObj is double d)
                    {
                        request.Temperature = (float)d;
                    }
                }
            }
        }

        return request;
    }
}
