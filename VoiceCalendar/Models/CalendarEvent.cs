using System;
using System.Linq;

namespace VoiceCalendar.Models;

public class CalendarEvent
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string? EventTime { get; set; }   // "HH:mm" 或 null（全天）
    public string? EndTime { get; set; }     // "HH:mm" 结束时间
    public bool IsRecurring { get; set; }
    public string? RecurrenceDays { get; set; }  // "0,2,4" = Mon/Wed/Fri (0=Monday)
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public CalendarEvent()
    {
        Id = Guid.NewGuid().ToString("N")[..8];
    }

    public CalendarEvent(string title, DateTime eventDate, string? eventTime = null)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Title = title;
        EventDate = eventDate.Date;
        EventTime = eventTime;
        CreatedAt = DateTime.Now;
    }

    public string TimeDisplay => !string.IsNullOrEmpty(EventTime) ? FormatTime12h(EventTime) : "全天";
    public string EndTimeDisplay => !string.IsNullOrEmpty(EndTime) ? $"结束 {FormatTime12h(EndTime)}" : "";

    private static string FormatTime12h(string hhmm)
    {
        if (hhmm.Length < 5) return hhmm;
        int h = int.Parse(hhmm.Substring(0, 2));
        int m = int.Parse(hhmm.Substring(3, 2));
        var ampm = h < 12 ? "上午" : "下午";
        var h12 = h % 12;
        if (h12 == 0) h12 = 12;
        return $"{ampm}{h12}:{m:D2}";
    }
    public bool HasTime => !string.IsNullOrEmpty(EventTime);
    public string RecurrenceDisplay => !IsRecurring || string.IsNullOrEmpty(RecurrenceDays)
        ? ""
        : "🔄 " + string.Join("", RecurrenceDays.Split(",").Select(d => (new[] { "周一","周二","周三","周四","周五","周六","周日" })[int.Parse(d)]));
}