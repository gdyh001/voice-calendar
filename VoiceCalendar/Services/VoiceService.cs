using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NAudio.Wave;
using Vosk;

namespace VoiceCalendar.Services;

public class VoiceService
{
    private Model? _model;
    private VoskRecognizer? _recognizer;
    private WaveInEvent? _waveIn;
    private string _accumulatedText = "";
    private readonly object _lock = new();
    private readonly string _modelPath;

    public bool IsListening { get; private set; }

    public VoiceService(string modelPath)
    {
        _modelPath = modelPath;
    }

    public bool Initialize()
    {
        if (_model != null) return true;
        if (!Directory.Exists(_modelPath)) return false;

        Vosk.Vosk.SetLogLevel(-1);
        _model = new Model(_modelPath);
        return true;
    }

    public string ModelPath => _modelPath;

    public async Task StartRecordingAsync()
    {
        if (_model == null && !Initialize())
            throw new InvalidOperationException(
                $"模型目录不存在: {_modelPath}`n请从 https://alphacephei.com/vosk/models 下载 vosk-model-small-cn-0.22`n解压到 VoiceCalendar/model/ 目录");

        IsListening = true;
        _accumulatedText = "";

        _recognizer = new VoskRecognizer(_model, 16000.0f);
        _recognizer.SetMaxAlternatives(0);
        _recognizer.SetWords(false);

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();

        await Task.CompletedTask;
    }

    public string StopRecording()
    {
        if (!IsListening) return "";

        lock (_lock)
        {
            try
            {
                _waveIn?.StopRecording();
                _waveIn?.Dispose();
            }
            catch { }
            _waveIn = null;

            if (_recognizer != null)
            {
                var final = _recognizer.FinalResult();
                var partialText = ParseText(final);
                if (!string.IsNullOrEmpty(partialText))
                    _accumulatedText += partialText;

                _recognizer.Dispose();
                _recognizer = null;
            }

            var result = string.IsNullOrEmpty(_accumulatedText)
                ? "[未检测到语音]"
                : _accumulatedText.Trim();

            IsListening = false;
            return result;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            if (_recognizer == null) return;

            if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                var json = _recognizer.Result();
                var text = ParseText(json);
                if (!string.IsNullOrEmpty(text))
                    _accumulatedText += text;
            }
        }
    }

    private static string ParseText(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("text").GetString();
            return text ?? "";
        }
        catch { return ""; }
    }
}
