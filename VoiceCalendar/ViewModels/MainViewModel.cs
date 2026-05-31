using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using VoiceCalendar.Models;
using VoiceCalendar.Services;
using System.Linq;

namespace VoiceCalendar.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly EventStorageService _storage = EventStorageService.Instance;
    private readonly NlpParserService _nlp = new();
    private readonly ReminderService _reminder;

    public MainViewModel()
    {
        SelectedDate = DateTime.Today;
        _reminder = new ReminderService();
        _reminder.OnRemind += OnReminderTriggered;
        _reminder.Start();
    }

    // === 属性 ===

    private DateTime _selectedDate;
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set { _selectedDate = value.Date; OnPropertyChanged(); RefreshEvents(); }
    }

    public ObservableCollection<CalendarEvent> Events { get; } = new();
    

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isListening;
    public bool IsListening
    {
        get => _isListening;
        set { _isListening = value; OnPropertyChanged(); OnPropertyChanged(nameof(VoiceButtonText)); }
    }

    public string VoiceButtonText => IsListening ? "正在聆听..." : "🎤 语音输入";

    // === 语音命令处理（由 MainWindow 调用）===

    public void ProcessVoiceCommand(string text)
    {
        var parsed = _nlp.Parse(text);
        var today = DateTime.Today;

        switch (parsed.Action)
        {
            case "add":
                var date = parsed.Date ?? today;
                var ev = _storage.AddEvent(parsed.Title, date, parsed.Time, parsed.RecurrenceDays);
                SelectedDate = date;
                StatusText = $"已添加: {ev.Title} ({date:yyyy-MM-dd} {ev.TimeDisplay})";
                break;

            case "delete":
                var targetDate = parsed.Date;
                int count = _storage.DeleteEventsByKeyword(parsed.Title, targetDate);
                if (count > 0)
                {
                    SelectedDate = targetDate ?? SelectedDate;
                    RefreshEvents();
                    StatusText = $"已删除 {count} 个匹配事件";
                }
                else
                {
                    StatusText = $"未找到「{parsed.Title}」相关事件";
                }
                break;

            case "query":
                if (parsed.Date.HasValue)
                    SelectedDate = parsed.Date.Value;
                else
                    RefreshEvents();
                break;
        }
    }

    // === 事件操作 ===

    public void RefreshEvents()
    {
        var list = _storage.GetEventsByDate(SelectedDate).OrderBy(e => e.EventTime ?? "99:99").ToList();
        Events.Clear(); foreach (var e in list) Events.Add(e);
        StatusText = list.Count > 0
            ? $"{SelectedDate:yyyy年MM月dd日} - {list.Count}个事件"
            : $"{SelectedDate:yyyy年MM月dd日} - 暂无事件";
    }

    public CalendarEvent AddEventManually(string title, DateTime date, string? time, string? endTime = null, string? recurrenceDays = null)
    {
        var ev = _storage.AddEvent(title, date, time, endTime, recurrenceDays);
        if (date.Date == SelectedDate.Date) RefreshEvents();
        StatusText = $"已添加: {title}";
        return ev;
    }

    public bool DeleteEvent(string id)
    {
        var ok = _storage.DeleteEvent(id);
        if (ok) RefreshEvents();
        return ok;
    }

    public CalendarEvent? UpdateEvent(string id, string title, DateTime date, string? time, string? endTime = null, string? recurrenceDays = null)
    {
        var ev = _storage.UpdateEvent(id, title, date, time, endTime, recurrenceDays);
        if (ev != null && date.Date == SelectedDate.Date) RefreshEvents();
        StatusText = ev != null ? $"已更新: {title}" : "更新失败";
        return ev;
    }

    

    public System.Collections.Generic.List<CalendarEvent> GetEventsForDate(DateTime date)
    {
        return _storage.GetEventsByDate(date);
    }
    private void OnReminderTriggered(CalendarEvent ev)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _reminder.ShowToast(ev);
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
