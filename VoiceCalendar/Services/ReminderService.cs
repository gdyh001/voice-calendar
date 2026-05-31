using System;
using System.Collections.Generic;
using System.Windows.Threading;
using VoiceCalendar.Models;

namespace VoiceCalendar.Services;

public class ReminderService
{
    private readonly DispatcherTimer _timer;
    private readonly EventStorageService _storage;
    private readonly HashSet<string> _notifiedIds = new();

    public event Action<CalendarEvent>? OnRemind;

    public ReminderService()
    {
        _storage = EventStorageService.Instance;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += CheckReminders;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void CheckReminders(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var currentTime = now.ToString("HH:mm");

        var events = _storage.GetAllEvents();
        foreach (var ev in events)
        {
            if (ev.EventTime == null) continue;
            if (ev.EventDate == today && ev.EventTime == currentTime)
            {
                if (!_notifiedIds.Contains(ev.Id))
                {
                    _notifiedIds.Add(ev.Id);
                    OnRemind?.Invoke(ev);
                }
            }
        }
    }

    public void ShowToast(CalendarEvent ev)
    {
        System.Windows.MessageBox.Show(
            $"{ev.TimeDisplay} - {ev.Title}",
            "日历提醒",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}