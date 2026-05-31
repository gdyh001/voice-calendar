# 语音识别模型

本项目使用 Sherpa-ONNX + Paraformer 中文小模型进行离线语音识别。

## 下载模型

从以下地址下载并解压到 `VoiceCalendar/model/paraformer-zh-small/` 目录：

https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-paraformer-zh-small-2024-03-09.tar.bz2

解压后目录应包含以下文件：
- tokens.txt
- encoder.int8.onnx
- decoder.int8.onnx

模型大小约 50MB，解压后约 55MB。