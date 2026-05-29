# 语音日历工具

通过语音交互管理日历事件的 Windows 桌面应用。支持语音添加、删除、查看事件，到期自动弹窗提醒。

## 功能

- 语音添加事件：说出时间和事件，自动添加到日历
- 语音删除事件：说出关键词，自动匹配并删除
- 语音查看事件：询问某天的安排，即时展示
- 文字编辑：键盘输入作为备选方案
- 到期提醒：事件到期时弹出 Windows 桌面通知

## 技术栈

- Python 3.10+
- tkinter (GUI)
- faster-whisper (语音识别)
- JSON (数据存储)

## 安装与运行

确保已安装 Python 3.10 或更高版本。

安装依赖：
```
pip install -r requirements.txt
```

运行：
```
python main.py
```

注意：首次运行会自动下载 faster-whisper small 模型（约 500MB），请确保网络通畅。后续使用无需联网。

## 项目结构

```
voice-calendar/
├── main.py
├── calendar_core.py
├── voice_engine.py
├── nlp_parser.py
├── reminder.py
├── requirements.txt
└── README.md
```