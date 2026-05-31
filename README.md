# G Day — 语音日历工具

通过中文语音交互管理日程的 Windows 桌面应用。说出时间与事件即可自动添加到日历，支持语音删除、查看、到期提醒，以及手动编辑和每周重复。

## 功能

### 语音交互
- **语音添加**：说出"明天下午三点开会"，自动解析时间并创建日程
- **语音删除**：说出"删除明天下午三点的会"，自动匹配并删除
- **语音查询**：说出"今天有什么安排"，展示当日所有日程
- 支持中文数字（二十三）、相对日期（大后天）、时间段（下午两点到三点）、每周重复（每周一三五）

### 日历
- 月视图日历，事件标记点显示
- 点击年份/月份打开快速跳转选择器（年份 2010-2050 滚动列表 + 月份网格）
- "今天"按钮一键回到当天
- 点击日期查看当日日程列表

### 日程管理
- 新建日程：标题、备注、开始/结束时间、每周重复（可选周一至周日）
- 日程卡片：显示开始时间、标题、结束时间、重复标记
- 鼠标悬停日程卡片浮现 ✎ 编辑 / ✕ 删除按钮
- 删除确认弹窗（iOS 风格遮罩 + 居中卡片）
- 日程按开始时间从早到晚排序
- 右键菜单已移除，改为悬浮按钮

### 时间选择器
- iOS 风格：上午/下午 + 小时 + 分钟 三栏下拉框
- 鼠标滚轮循环切换选项，无需点开下拉列表
- 点击开始/结束时间行展开选择器

### 提醒系统
- 每分钟轮询检查到期事件
- 事件到期弹出桌面通知

## 技术栈

- .NET 9 WPF（`net9.0-windows10.0.19041.0`）
- WinRT SpeechRecognizer（Windows 内置语音引擎，无需联网）
- 自研中文 NLP 解析器（日期、时间、时间段、重复关键词）
- JSON 文件本地存储
- MVVM 架构（MainViewModel + Services + Models）

## 项目结构

```
VoiceCalendar/
├── App.xaml / App.xaml.cs          # 应用入口 + 全局样式
├── MainWindow.xaml / .xaml.cs      # 主窗口 UI + 交互逻辑
├── Controls/
│   └── CalendarView.xaml / .cs     # 日历月视图 + 年月选择器 + DayCell
├── ViewModels/
│   └── MainViewModel.cs            # 数据绑定 + 事件 CRUD + 语音处理
├── Models/
│   └── CalendarEvent.cs            # 日程数据模型
├── Services/
│   ├── VoiceService.cs             # WinRT 语音识别封装
│   ├── NlpParserService.cs         # 中文 NLP 解析（日期/时间/重复）
│   ├── EventStorageService.cs      # JSON 文件持久化
│   └── ReminderService.cs          # 定时提醒 + 桌面通知
├── Converters/
│   └── BoolToVisibilityConverter.cs
└── VoiceCalendar.csproj
```

## 运行

### 前提
- Windows 10 19041+ 或 Windows 11
- .NET 9 SDK
- Windows 中文语音包（设置 → 时间和语言 → 语音 → 添加中文语音）

### 构建与启动

```powershell
cd VoiceCalendar
dotnet build
dotnet run
```

首次运行会自动创建 `events.json` 存储文件。

## 语音指令示例

| 语音输入 | 效果 |
|---|---|
| "明天下午三点开会" | 添加明天 15:00 的日程"开会" |
| "每周一上午十点站会" | 添加每周一 10:00 的重复日程 |
| "后天下午两点到四点培训" | 添加后天 14:00-16:00 的日程 |
| "删除明天下午三点的会" | 删除匹配的日程 |
| "今天有什么安排" | 显示今天的日程列表 |
| "六月三号晚上八点聚餐" | 添加 6月3日 20:00 的日程 |