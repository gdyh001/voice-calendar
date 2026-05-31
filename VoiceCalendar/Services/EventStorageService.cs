using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VoiceCalendar.Models;

namespace VoiceCalendar.Services;

public class EventStorageService
{
    public static readonly EventStorageService Instance = new();
    private static readonly string DataFile =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "events.json");

    private List<CalendarEvent>? _cache;
    private bool _dirty;

    public List<CalendarEvent> GetAllCached()
    {
        EnsureLoaded();
        return _cache!;
    }

    private void EnsureLoaded()
    {
        if (_cache != null) return;
        if (!File.Exists(DataFile)) { _cache = new List<CalendarEvent>(); return; }
        try
        {
            var json = File.ReadAllText(DataFile);
            _cache = JsonSerializer.Deserialize<List<CalendarEvent>>(json) ?? new List<CalendarEvent>();
        }
        catch
        {
            _cache = new List<CalendarEvent>();
        }
    }

    private void MarkDirty() => _dirty = true;

    public void SaveIfDirty()
    {
        if (!_dirty || _cache == null) return;
        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DataFile, json);
        _dirty = false;
    }

    public CalendarEvent AddEvent(string title, DateTime eventDate, string? eventTime = null, string? endTime = null, string? recurrenceDays = null)
    {
        EnsureLoaded();
        var ev = new CalendarEvent(title, eventDate.Date, eventTime) { EndTime = endTime };
        if (!string.IsNullOrEmpty(recurrenceDays))
        {
            ev.IsRecurring = true;
            ev.RecurrenceDays = recurrenceDays;
        }
        _cache!.Add(ev);
        MarkDirty();
        return ev;
    }

    public bool DeleteEvent(string id)
    {
        EnsureLoaded();
        var count = _cache!.RemoveAll(e => e.Id == id);
        if (count > 0) MarkDirty();
        return count > 0;
    }

    public int DeleteEventsByKeyword(string keyword, DateTime? targetDate = null)
    {
        EnsureLoaded();
        var removed = _cache!.RemoveAll(e =>
            e.Title.Contains(keyword) &&
            (!targetDate.HasValue || e.EventDate == targetDate.Value.Date));
        if (removed > 0) MarkDirty();
        return removed;
    }

    public List<CalendarEvent> GetEventsByDate(DateTime date)
    {
        EnsureLoaded();
        int dow = ((int)date.DayOfWeek + 6) % 7; // 0=Monday
        string dowStr = dow.ToString();
        return _cache!
            .Where(e => e.EventDate == date.Date ||
                (e.IsRecurring && e.RecurrenceDays != null && e.RecurrenceDays.Split(',').Contains(dowStr) && e.EventDate <= date.Date))
            .OrderBy(e => e.EventTime ?? "00:00")
            .ToList();
    }

    public List<CalendarEvent> GetAllEvents()
    {
        EnsureLoaded();
        return _cache!
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.EventTime ?? "00:00")
            .ToList();
    }

    public List<CalendarEvent> GetUpcomingEvents(int limit = 10)
    {
        var today = DateTime.Today;
        EnsureLoaded();
        return _cache!
            .Where(e => e.EventDate >= today)
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.EventTime ?? "00:00")
            .Take(limit)
            .ToList();
    }

    public CalendarEvent? UpdateEvent(string id, string? title = null,
        DateTime? eventDate = null, string? eventTime = null, string? endTime = null, string? recurrenceDays = null)
    {
        EnsureLoaded();
        var ev = _cache!.FirstOrDefault(e => e.Id == id);
        if (ev == null) return null;
        if (title != null) ev.Title = title;
        if (eventDate.HasValue) ev.EventDate = eventDate.Value.Date;
        if (eventTime != null) ev.EventTime = eventTime;
        if (endTime != null) ev.EndTime = endTime;
        if (recurrenceDays != null)
        {
            ev.IsRecurring = !string.IsNullOrEmpty(recurrenceDays);
            ev.RecurrenceDays = string.IsNullOrEmpty(recurrenceDays) ? null : recurrenceDays;
        }
        MarkDirty();
        return ev;
    }

    public bool HasEventsOnDate(DateTime date) => GetEventsByDate(date).Count > 0;

    public Dictionary<int, int> GetEventCountsForMonth(int year, int month)
    {
        EnsureLoaded();
        var counts = new Dictionary<int, int>();
        foreach (var e in _cache!)
        {
            if (e.EventDate.Year == year && e.EventDate.Month == month)
            {
                int day = e.EventDate.Day;
                counts[day] = counts.GetValueOrDefault(day) + 1;
            }
            if (e.IsRecurring && e.RecurrenceDays != null)
            {
                var start = e.EventDate;
                var end = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                if (start > end) continue;
                var current = start > new DateTime(year, month, 1) ? start : new DateTime(year, month, 1);
                while (current <= end)
                {
                    int cdow = ((int)current.DayOfWeek + 6) % 7;
                    if (e.RecurrenceDays.Split(',').Contains(cdow.ToString()))
                        counts[current.Day] = counts.GetValueOrDefault(current.Day) + 1;
                    current = current.AddDays(1);
                }
            }
        }
        return counts;
    }
}
