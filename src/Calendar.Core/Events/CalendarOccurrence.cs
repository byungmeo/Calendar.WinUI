using Calendar.Core.Dates;

namespace Calendar.Core.Events;

/// <summary>A projected occurrence retaining its caller-owned source and unclipped metadata.</summary>
public sealed class CalendarOccurrence
{
    private CalendarOccurrence(object source, CalendarEventData data, string text, int index, DateRange dates, DateTime localStart, object? key)
    {
        Source = source; Data = data; DisplayText = text; SourceIndex = index;
        Dates = dates; LocalStart = localStart; Key = key;
    }
    /// <summary>Gets the original application object.</summary>
    public object Source { get; }
    /// <summary>Gets the adapter-supplied stable occurrence key, or null for source reference identity.</summary>
    public object? Key { get; }
    /// <summary>Gets the original, unclipped occurrence data.</summary>
    public CalendarEventData Data { get; }
    /// <summary>Gets the text supplied by the adapter.</summary>
    public string DisplayText { get; }
    /// <summary>Gets the zero-based input order used to break sorting ties.</summary>
    public int SourceIndex { get; }
    /// <summary>Gets the dates occupied in the display time zone, with an exclusive end.</summary>
    public DateRange Dates { get; }
    /// <summary>Gets the displayed wall-clock start used by the month layout.</summary>
    public DateTime LocalStart { get; }
    /// <summary>Gets whether this is an all-day occurrence.</summary>
    public bool IsAllDay => Data.IsAllDay;

    /// <summary>Projects one application occurrence into a display time zone while preserving the source object.</summary>
    public static CalendarOccurrence Project(object source, CalendarEventData data, string text, int sourceIndex, TimeZoneInfo displayTimeZone)
        => Project(source, data, text, sourceIndex, displayTimeZone, null);

    /// <summary>Projects an occurrence with an optional stable application key used to preserve selection.</summary>
    public static CalendarOccurrence Project(object source, CalendarEventData data, string text, int sourceIndex, TimeZoneInfo displayTimeZone, object? key)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(displayTimeZone);
        if (data.AllDayDates is { } allDay)
            return new(source, data, text, sourceIndex, allDay, allDay.Start.ToDateTime(TimeOnly.MinValue), key);

        DateTime start;
        DateTime? end;
        if (data.TimeKind == CalendarTimeKind.Floating)
        {
            start = data.FloatingStart!.Value;
            end = data.FloatingEnd;
        }
        else
        {
            start = TimeZoneInfo.ConvertTime(data.Start!.Value, displayTimeZone).DateTime;
            end = data.End.HasValue ? TimeZoneInfo.ConvertTime(data.End.Value, displayTimeZone).DateTime : null;
        }
        var first = DateOnly.FromDateTime(start);
        var lastExclusive = end.HasValue
            ? DateOnly.FromDateTime(end.Value).AddDays(end.Value.TimeOfDay == TimeSpan.Zero ? 0 : 1)
            : first.AddDays(1);
        return new(source, data, text, sourceIndex, new(first, lastExclusive), start, key);
    }

    /// <summary>All-day source order, followed by displayed wall-clock start and source order.</summary>
    public static IReadOnlyList<CalendarOccurrence> Sort(IEnumerable<CalendarOccurrence> occurrences) =>
        Array.AsReadOnly(occurrences.OrderByDescending(x => x.IsAllDay)
            .ThenBy(x => x.IsAllDay ? DateTime.MinValue : x.LocalStart)
            .ThenBy(x => x.SourceIndex).ToArray());
}
