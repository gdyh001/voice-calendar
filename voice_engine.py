"""
语音引擎模块：麦克风录音 + faster-whisper 语音转文字

使用 sounddevice 录制音频，faster-whisper 进行离线语音识别。
首次运行会自动下载 small 模型（约 500MB）。
"""

import os
import tempfile
import threading
from typing import Optional

import numpy as np

# 采样率和录音参数
SAMPLE_RATE = 16000
CHANNELS = 1
MAX_RECORD_SECONDS = 15

# 模型实例（懒加载，全局复用）
_model = None
_model_lock = threading.Lock()


def _get_model():
    """获取 faster-whisper 模型实例（懒加载、线程安全）"""
    global _model
    if _model is not None:
        return _model
    with _model_lock:
        if _model is not None:
            return _model
        from faster_whisper import WhisperModel
        _model = WhisperModel("small", device="cpu", compute_type="int8")
        return _model


def record_audio(duration: Optional[float] = None,
                 max_duration: float = MAX_RECORD_SECONDS) -> Optional[str]:
    """
    录制音频并保存为临时 WAV 文件。
    duration: 固定录音时长（秒），None 表示使用 max_duration
    返回: 临时 WAV 文件路径，失败返回 None
    """
    try:
        import sounddevice as sd
        from scipy.io import wavfile
    except ImportError as e:
        raise ImportError("请先安装依赖: pip install sounddevice scipy") from e

    # 查找输入设备
    try:
        devices = sd.query_devices()
    except Exception:
        devices = []

    input_device = None
    for i, d in enumerate(devices):
        if d.get("max_input_channels", 0) > 0:
            input_device = i
            break

    if input_device is None:
        raise RuntimeError("未检测到麦克风设备，请检查麦克风连接")

    record_seconds = min(duration or max_duration, max_duration)

    try:
        chunk_samples = int(SAMPLE_RATE * record_seconds)
        recording = sd.rec(
            chunk_samples,
            samplerate=SAMPLE_RATE,
            channels=CHANNELS,
            dtype="float32",
            device=input_device,
        )
        sd.wait()
    except Exception as e:
        raise RuntimeError("录音失败: " + str(e)) from e

    audio_data = np.squeeze(recording)
    if audio_data.dtype == np.float32:
        audio_data = (audio_data * 32767).astype(np.int16)

    tmp_path = os.path.join(tempfile.gettempdir(), "voice_calendar.wav")
    wavfile.write(tmp_path, SAMPLE_RATE, audio_data)
    return tmp_path


def transcribe_audio(audio_path: str, language: str = "zh") -> str:
    """
    将音频文件转为文字。
    language: "zh" 表示中文
    返回识别文字，失败返回错误信息字符串
    """
    if not os.path.exists(audio_path):
        return ""

    try:
        model = _get_model()
    except Exception as e:
        return "[模型加载失败: " + str(e) + "]"

    try:
        segments, _ = model.transcribe(audio_path, language=language, beam_size=5)
        text_parts = []
        for segment in segments:
            text_parts.append(segment.text.strip())
        return "".join(text_parts)
    except Exception as e:
        return "[识别失败: " + str(e) + "]"


def speech_to_text(duration: Optional[float] = None) -> str:
    """
    一站式语音转文字：录音 + 识别。
    duration: 录音时长（秒），默认 5 秒
    返回识别的文字
    """
    if duration is None:
        duration = 5.0

    audio_path = record_audio(duration=duration)
    if audio_path is None:
        return "[录音失败]"

    try:
        return transcribe_audio(audio_path)
    finally:
        try:
            os.remove(audio_path)
        except OSError:
            pass


def cleanup_model():
    """释放模型资源"""
    global _model
    _model = None
    import gc
    gc.collect()