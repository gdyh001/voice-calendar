using System;

namespace VoiceCalendar.Models;

public class CalendarEvent
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string? EventTime { get; set; }   // "HH:mm" 或 null（全天）
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

    public string TimeDisplay => EventTime ?? "全天";
    public bool HasTime => !string.IsNullOrEmpty(EventTime);
}