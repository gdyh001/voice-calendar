using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using VoiceCalendar.Models;

namespace VoiceCalendar.Services;

public class EventStorageService
{
    private static readonly string DataFile =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "events.json");

    private List<CalendarEvent> LoadEvents()
    {
        if (!File.Exists(DataFile)) return new List<CalendarEvent>();
        try
        {
            var json = File.ReadAllText(DataFile);
            return JsonConvert.DeserializeObject<List<CalendarEvent>>(json) ?? new List<CalendarEvent>();
        }
        catch
        {
            return new List<CalendarEvent>();
        }
    }

    private void SaveEvents(List<CalendarEvent> events)
    {
        var json = JsonConvert.SerializeObject(events, Formatting.Indented);
        File.WriteAllText(DataFile, json);
    }

    public CalendarEvent AddEvent(string title, DateTime eventDate, string? eventTime = null)
    {
        var ev = new CalendarEvent(title, eventDate.Date, eventTime);
        var events = LoadEvents();
        events.Add(ev);
        SaveEvents(events);
        return ev;
    }

    public bool DeleteEvent(string id)
    {
        var events = LoadEvents();
        var count = events.RemoveAll(e => e.Id == id);
        if (count > 0) SaveEvents(events);
        return count > 0;
    }

    public int DeleteEventsByKeyword(string keyword, DateTime? targetDate = null)
    {
        var events = LoadEvents();
        var removed = events.RemoveAll(e =>
            e.Title.Contains(keyword) &&
            (!targetDate.HasValue || e.EventDate == targetDate.Value.Date));
        if (removed > 0) SaveEvents(events);
        return removed;
    }

    public List<CalendarEvent> GetEventsByDate(DateTime date)
    {
        return LoadEvents()
            .Where(e => e.EventDate == date.Date)
            .OrderBy(e => e.EventTime ?? "00:00")
            .ToList();
    }

    public List<CalendarEvent> GetAllEvents()
    {
        return LoadEvents()
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.EventTime ?? "00:00")
            .ToList();
    }

    public List<CalendarEvent> GetUpcomingEvents(int limit = 10)
    {
        var today = DateTime.Today;
        return LoadEvents()
            .Where(e => e.EventDate >= today)
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.EventTime ?? "00:00")
            .Take(limit)
            .ToList();
    }

    public CalendarEvent? UpdateEvent(string id, string? title = null,
        DateTime? eventDate = null, string? eventTime = null)
    {
        var events = LoadEvents();
        var ev = events.FirstOrDefault(e => e.Id == id);
        if (ev == null) return null;
        if (title != null) ev.Title = title;
        if (eventDate.HasValue) ev.EventDate = eventDate.Value.Date;
        if (eventTime != null) ev.EventTime = eventTime;
        SaveEvents(events);
        return ev;
    }

    public bool HasEventsOnDate(DateTime date)
    {
        return GetEventsByDate(date).Count > 0;
    }

    public Dictionary<int, int> GetEventCountsForMonth(int year, int month)
    {
        var events = GetAllEvents();
        var counts = new Dictionary<int, int>();
        foreach (var e in events)
        {
            if (e.EventDate.Year == year && e.EventDate.Month == month)
            {
                int day = e.EventDate.Day;
                counts[day] = counts.GetValueOrDefault(day) + 1;
            }
        }
        return counts;
    }
}