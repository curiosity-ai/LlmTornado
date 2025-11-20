using LlmTornado.Audio;
using LlmTornado.Audio.Models;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.Game;

public static class TTS_Controller
{
    private static TornadoApi? _client;
    private static WaveOutEvent? _currentOutputDevice;
    private static readonly string ttsFilePath = "ttsdemo.mp3";
    public static bool IsEnabled { get; set; } = true;

    public static bool EnableDebugLogs = false;

    public static SpeechVoice Voice = SpeechVoice.Ash;
    public static AudioModel VoiceModel = AudioModel.OpenAi.Gpt4.Gpt4OMiniTts;
    public static SpeechResponseFormat VoiceFormat = SpeechResponseFormat.Mp3;
    public static string VoiceStartMessage = "\n[Audio playing /rest, /skip or any action to stop tts]";

    public static string VoiceInstructions = @"Voice Affect: Low, hushed, and suspenseful; convey tension and intrigue.

Tone: Deeply serious and mysterious, maintaining an undercurrent of unease throughout.

Pacing: Fast, deliberate.

Emotion: Restrained yet intense—voice should subtly tremble or tighten at key suspenseful points.

Emphasis: Highlight sensory descriptions (""footsteps echoed,"" ""heart hammering,"" ""shadows melting into darkness"") to amplify atmosphere.

Pronunciation: Clear and cunning.

Pauses: Limit pausing to keep up pace but Insert meaningful pauses for dramatic moments.";

    public static void Initialize(TornadoApi client)
    {
        _client = client;
    }

    public static async Task CreateTTS(string text)
    {
        if(_client is null)
        {
            IsEnabled = false;
            throw new InvalidOperationException("TTS_Controller not initialized with TornadoApi client.");
        }

        SpeechTtsResult? result = await _client.Audio.CreateSpeech(new SpeechRequest
        {
            Input = text,
            Model = VoiceModel,
            ResponseFormat = VoiceFormat,
            Voice = Voice,
            Instructions = VoiceInstructions
        });

        if (result is not null)
        {
            await result.SaveAndDispose(ttsFilePath);
        }
    }

    public static void StartTTS()
    {
        if (!IsEnabled)
        {
            return;
        }
        
        if(File.Exists(ttsFilePath) == false)
        {
            if(EnableDebugLogs)
                Console.WriteLine("[TTS file not found]");
            return;
        }

        try
        {
            Console.Write(VoiceStartMessage);

            TimeSpan duration = GetWavFileDuration(ttsFilePath);

            using (var audioFile = new AudioFileReader(ttsFilePath))
            {
                _currentOutputDevice = new WaveOutEvent();
                _currentOutputDevice.Init(audioFile);
                _currentOutputDevice.Play();

                while (_currentOutputDevice.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(100);
                }

                _currentOutputDevice?.Dispose();
                _currentOutputDevice = null;
            }
        }
        catch (Exception ex)
        {
            // Handle any TTS errors gracefully (e.g., file not found)
            if (EnableDebugLogs)
                Console.WriteLine($"[TTS unavailable: {ex.Message}]");
        }
    }

    public static void StopTTS()
    {
        try
        {
            if (_currentOutputDevice != null && _currentOutputDevice.PlaybackState == PlaybackState.Playing)
            {
                _currentOutputDevice.Stop();
                if (EnableDebugLogs)
                    Console.WriteLine("[Audio stopped]");
            }
        }
        catch (Exception ex)
        {
            // Silently handle any errors
        }
    }

    private static TimeSpan GetWavFileDuration(string fileName)
    {
        using (var audioFile = new AudioFileReader(fileName))
        {
            return audioFile.TotalTime;
        }
    }

}
