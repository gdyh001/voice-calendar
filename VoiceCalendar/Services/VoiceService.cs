using System;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;

namespace VoiceCalendar.Services;

public class VoiceService
{
    private SpeechRecognizer? _recognizer;
    private string _accumulatedText = "";

    public bool IsListening { get; private set; }

    public async Task StartRecordingAsync()
    {
        IsListening = true;
        _accumulatedText = "";

        _recognizer = new SpeechRecognizer(new Windows.Globalization.Language("zh-CN"));
        await _recognizer.CompileConstraintsAsync();

        _recognizer.ContinuousRecognitionSession.ResultGenerated += (_, args) =>
        {
            var text = args.Result.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
                _accumulatedText += text;
        };

        await _recognizer.ContinuousRecognitionSession.StartAsync(
            SpeechContinuousRecognitionMode.Default);
    }

    public string StopRecording()
    {
        if (!IsListening) return "";

        try
        {
            _recognizer?.ContinuousRecognitionSession.StopAsync();
        }
        catch { }

        var result = string.IsNullOrEmpty(_accumulatedText) ? "[未检测到语音]" : _accumulatedText;

        IsListening = false;
        try { _recognizer?.Dispose(); } catch { }
        _recognizer = null;

        return result;
    }
}