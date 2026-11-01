using Calendar.Core.Dates;

namespace Calendar.Core.Events;

/// <summary>Distinguishes calendar dates, absolute instants, and floating wall-clock times.</summary>
public enum CalendarTimeKind
{
    /// <summary>A date-only interval that does not move with the display time zone.</summary>
    AllDay,
    /// <summary>An absolute instant or interval projected into the display time zone.</summary>
    Absolute,
    /// <summary>A wall-clock time or interval without a time zone.</summary>
    Floating
}

/// <summary>One occurrence, preserving DATE, absolute time, or floating time semantics.
/// Recurrence expansion and custom VTIMEZONE resolution belong to the caller.</summary>
public sealed record CalendarEventData
{
    private CalendarEventData() { }

    /// <summary>Gets the time semantics of this occurrence.</summary>
    public CalendarTimeKind TimeKind { get; private init; }
    /// <summary>Gets the date-only interval for an all-day occurrence, or null.</summary>
    public DateRange? AllDayDates { get; private init; }
    /// <summary>Gets the absolute start instant, or null for non-absolute events.</summary>
    public DateTimeOffset? Start { get; private init; }
    /// <summary>Gets the exclusive absolute end instant, or null for a point event or non-absolute event.</summary>
    public DateTimeOffset? End { get; private init; }
    /// <summary>Gets the unspecified wall-clock start, or null for non-floating events.</summary>
    public DateTime? FloatingStart { get; private init; }
    /// <summary>Gets the exclusive wall-clock end, or null for a point event or non-floating event.</summary>
    public DateTime? FloatingEnd { get; private init; }
    /// <summary>Gets the optional original iCalendar UID metadata.</summary>
    public string? Uid { get; init; }
    /// <summary>Gets optional metadata identifying an original recurrence instance.</summary>
    public string? RecurrenceId { get; init; }
    /// <summary>Gets the optional original time zone identifier; this value is not resolved by the control.</summary>
    public string? TimeZoneId { get; init; }
    /// <summary>Gets whether this is a date-only occurrence.</summary>
    public bool IsAllDay => TimeKind == CalendarTimeKind.AllDay;
    /// <summary>Gets whether this is a non-all-day occurrence without an explicit end.</summary>
    public bool IsInstant => !IsAllDay && (TimeKind == CalendarTimeKind.Absolute ? End is null : FloatingEnd is null);

    /// <summary>Creates an all-day occurrence. Omitting the exclusive end produces one day.</summary>
    public static CalendarEventData AllDay(DateOnly start, DateOnly? endExclusive = null) => new()
    {
        TimeKind = CalendarTimeKind.AllDay,
        AllDayDates = new DateRange(start, endExclusive ?? start.AddDays(1))
    };

    /// <summary>Omit end for a point event; an explicit end must follow start.</summary>
    public static CalendarEventData Timed(DateTimeOffset start, DateTimeOffset? end = null)
    {
        if (end.HasValue && end.Value <= start)
            throw new ArgumentOutOfRangeException(nameof(end), "An explicit end must follow start.");
        return new() { TimeKind = CalendarTimeKind.Absolute, Start = start, End = end };
    }

    /// <summary>Floating wall-clock time is never converted to the display time zone.</summary>
    public static CalendarEventData Floating(DateTime start, DateTime? end = null)
    {
        if (start.Kind != DateTimeKind.Unspecified || (end.HasValue && end.Value.Kind != DateTimeKind.Unspecified))
            throw new ArgumentException("Floating times must have DateTimeKind.Unspecified.");
        if (end.HasValue && end.Value <= start)
            throw new ArgumentOutOfRangeException(nameof(end), "An explicit end must follow start.");
        return new() { TimeKind = CalendarTimeKind.Floating, FloatingStart = start, FloatingEnd = end };
    }
}
