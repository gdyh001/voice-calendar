using System;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;

namespace VoiceCalendar.Services;

public class VoiceService
{
    private SpeechRecognizer? _recognizer;
    private TaskCompletionSource<string>? _tcs;

    public bool IsListening { get; private set; }

    public async Task<string> ListenAsync(int timeoutMs = 8000)
    {
        if (IsListening) return "[正在聆听中...]";

        _tcs = new TaskCompletionSource<string>();
        IsListening = true;

        try
        {
            _recognizer = new SpeechRecognizer(
                new Windows.Globalization.Language("zh-CN"));

            await _recognizer.CompileConstraintsAsync();

            _recognizer.ContinuousRecognitionSession.ResultGenerated += (_, args) =>
            {
                var text = args.Result.Text?.Trim();
                if (!string.IsNullOrEmpty(text) && !_tcs.Task.IsCompleted)
                    _tcs.TrySetResult(text);
            };

            _recognizer.ContinuousRecognitionSession.Completed += (_, _) =>
            {
                if (!_tcs.Task.IsCompleted)
                    _tcs.TrySetResult("[识别结束]");
            };

            await _recognizer.ContinuousRecognitionSession.StartAsync(
                SpeechContinuousRecognitionMode.Default);

            var timeout = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(_tcs.Task, timeout);

            if (completed == timeout)
            {
                try { await _recognizer.ContinuousRecognitionSession.StopAsync(); } catch { }
                return "[超时：未检测到语音]";
            }

            return await _tcs.Task;
        }
        catch (UnauthorizedAccessException)
        {
            return "[请开启麦克风权限：设置→隐私→麦克风]";
        }
        catch (Exception ex) when (ex.HResult == -2147024809 ||
                                    ex.Message.Contains("privacy"))
        {
            return "[请开启语音识别：设置→隐私→语音→打开\"在线语音识别\"]";
        }
        catch (Exception ex)
        {
            return "[语音引擎: " + ex.Message + "]";
        }
        finally
        {
            IsListening = false;
            if (_recognizer != null)
            {
                try { _recognizer.Dispose(); } catch { }
                _recognizer = null;
            }
        }
    }
}