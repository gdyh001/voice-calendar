using System;
using System.Speech.Recognition;
using System.Threading.Tasks;

namespace VoiceCalendar.Services;

public class VoiceService
{
    private SpeechRecognitionEngine? _engine;
    private TaskCompletionSource<string>? _tcs;

    public bool IsListening { get; private set; }

    public async Task<string> ListenAsync(int timeoutMs = 5000)
    {
        if (IsListening) return "[正在聆听中...]";

        _tcs = new TaskCompletionSource<string>();
        IsListening = true;

        try
        {
            _engine = new SpeechRecognitionEngine();

            // 尝试加载中文听写引擎
            try
            {
                _engine.SetInputToDefaultAudioDevice();
                _engine.LoadGrammar(new DictationGrammar());
            }
            catch (Exception ex)
            {
                return $"[语音引擎初始化失败: {ex.Message}。请确保已安装中文语音包(设置→时间和语言→语音→添加语言→中文)]";
            }

            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.SpeechHypothesized += OnSpeechHypothesized;
            _engine.RecognizeCompleted += (_, _) =>
            {
                if (!_tcs.Task.IsCompleted)
                    _tcs.TrySetResult("");
            };

            _engine.RecognizeAsync(RecognizeMode.Single);

            // 超时控制
            var timeout = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(_tcs.Task, timeout);

            if (completed == timeout)
            {
                _engine.RecognizeAsyncCancel();
                return "[未检测到语音，请重试]";
            }

            return await _tcs.Task;
        }
        catch (Exception ex)
        {
            return $"[错误: {ex.Message}]";
        }
        finally
        {
            IsListening = false;
            if (_engine != null)
            {
                _engine.SpeechRecognized -= OnSpeechRecognized;
                _engine.SpeechHypothesized -= OnSpeechHypothesized;
                try { _engine.Dispose(); } catch { }
                _engine = null;
            }
        }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result != null && e.Result.Confidence > 0.3)
        {
            _tcs?.TrySetResult(e.Result.Text);
        }
    }

    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        // 可用于显示实时识别进度
    }
}