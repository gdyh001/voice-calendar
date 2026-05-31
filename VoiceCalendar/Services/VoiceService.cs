using System;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;

namespace VoiceCalendar.Services;

public class VoiceService
{
    private SpeechRecognizer? _recognizer;
    private bool _isListening;
    private string _finalText = "";

    public bool IsListening => _isListening;
    public event Action<string>? PartialResultChanged;

    public VoiceService(string _) { }

    public bool Initialize()
    {
        try { _recognizer = new SpeechRecognizer(); return true; }
        catch { return false; }
    }

    public async Task StartRecordingAsync()
    {
        if (_recognizer == null && !Initialize())
            throw new InvalidOperationException(
                "Windows 语音引擎未就绪，请安装中文语音包（设置 → 时间和语言 → 语音）");

        _isListening = true;
        _finalText = "";

        _recognizer!.Constraints.Clear();
        _recognizer.Constraints.Add(
            new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dict"));
        await _recognizer.CompileConstraintsAsync();

        _recognizer.HypothesisGenerated += OnHypothesis;
        _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResult;

        await _recognizer.ContinuousRecognitionSession.StartAsync();
    }

    private void OnHypothesis(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs e)
    {
        if (_isListening && !string.IsNullOrEmpty(e.Hypothesis.Text))
            PartialResultChanged?.Invoke(e.Hypothesis.Text);
    }

    private void OnResult(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs e)
    {
        if (_isListening && e.Result.Status == SpeechRecognitionResultStatus.Success)
            _finalText = e.Result.Text?.Trim() ?? "";
    }

    public async Task<string> StopRecordingAsync()
    {
        if (!_isListening) return "";
        _isListening = false;

        try
        {
            if (_recognizer != null)
            {
                _recognizer.HypothesisGenerated -= OnHypothesis;
                _recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResult;
                await _recognizer.ContinuousRecognitionSession.StopAsync();
            }
            return string.IsNullOrEmpty(_finalText) ? "[未检测到语音]" : _finalText;
        }
        catch
        {
            return "[未检测到语音]";
        }
    }

    public void DisposeModel() { _recognizer?.Dispose(); _recognizer = null; }
    public bool IsModelLoaded => _recognizer != null;
}