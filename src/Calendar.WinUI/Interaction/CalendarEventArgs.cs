using Calendar.Core.Events;
using Calendar.Core.Views;
using Microsoft.UI.Xaml;

namespace Calendar.WinUI.Interaction;

/// <summary>Contains a date, its relationship to the primary range, and an optional UI anchor.</summary>
public class CalendarDateEventArgs : EventArgs
{
    /// <summary>Contains a date, its relationship to the primary range, and an optional UI anchor.</summary>
    public CalendarDateEventArgs(DateTimeOffset date, bool outside, FrameworkElement? anchor = null)
    { Date = date; IsOutsidePrimaryRange = outside; Anchor = anchor; }
    /// <summary>Date-only transport: calendar components are significant, not the UTC offset.</summary>
    public DateTimeOffset Date { get; }
    /// <summary>Gets the calendar date without interpreting the transport value as a UTC instant.</summary>
    public DateOnly DateOnly => System.DateOnly.FromDateTime(Date.Date);
    /// <summary>Gets whether the invoked date lies outside the primary displayed month.</summary>
    public bool IsOutsidePrimaryRange { get; }
    /// <summary>Gets the UI element that the application can use as a popup anchor.</summary>
    public FrameworkElement? Anchor { get; }
}

/// <summary>Contains an invoked occurrence and its original application object.</summary>
public sealed class CalendarEventInvokedEventArgs : CalendarDateEventArgs
{
    /// <summary>Contains an invoked occurrence and its original application object.</summary>
    public CalendarEventInvokedEventArgs(DateTimeOffset date, bool outside, CalendarOccurrence occurrence, FrameworkElement anchor)
        : base(date, outside, anchor) => Occurrence = occurrence;
    /// <summary>Gets the selected original occurrence, rather than a clipped display segment.</summary>
    public CalendarOccurrence Occurrence { get; }
    /// <summary>Gets the original application object associated with the occurrence.</summary>
    public object Source => Occurrence.Source;
}

/// <summary>Contains all occurrences on an invoked date, including visible occurrences.</summary>
public sealed class CalendarMoreInvokedEventArgs : CalendarDateEventArgs
{
    /// <summary>Contains all occurrences on an invoked date, including visible occurrences.</summary>
    public CalendarMoreInvokedEventArgs(DateTimeOffset date, bool outside, IReadOnlyList<CalendarOccurrence> events, FrameworkElement anchor)
        : base(date, outside, anchor) => Events = events;
    /// <summary>All occurrences on the date, in display order, including visible ones.</summary>
    public IReadOnlyList<CalendarOccurrence> Events { get; }
}

/// <summary>Contains the previous and new selected calendar dates.</summary>
public sealed class CalendarSelectionChangedEventArgs(DateTimeOffset? oldDate, DateTimeOffset? newDate) : EventArgs
{
    /// <summary>Gets the previous selected date, or null.</summary>
    public DateTimeOffset? OldDate { get; } = oldDate;
    /// <summary>Gets the new selected date, or null.</summary>
    public DateTimeOffset? NewDate { get; } = newDate;
}

/// <summary>Contains the previous and new displayed date ranges.</summary>
public sealed class CalendarRangeChangedEventArgs(CalendarViewRange? oldRange, CalendarViewRange newRange) : EventArgs
{
    /// <summary>Gets the previous ranges, or null before the first range notification.</summary>
    public CalendarViewRange? OldRange { get; } = oldRange;
    /// <summary>Gets the current primary and visible ranges.</summary>
    public CalendarViewRange NewRange { get; } = newRange;
}

/// <summary>Contains the previous and new selected occurrences.</summary>
public sealed class CalendarEventSelectionChangedEventArgs(CalendarOccurrence? oldEvent, CalendarOccurrence? newEvent) : EventArgs
{
    /// <summary>Gets the previously selected occurrence, or null.</summary>
    public CalendarOccurrence? OldEvent { get; } = oldEvent;
    /// <summary>Gets the newly selected occurrence, or null.</summary>
    public CalendarOccurrence? NewEvent { get; } = newEvent;
}

/// <summary>Contains the previous and new original selected application objects.</summary>
public sealed class CalendarItemSelectionChangedEventArgs(object? oldItem, object? newItem) : EventArgs
{
    /// <summary>Gets the previous original application object, or null.</summary>
    public object? OldItem { get; } = oldItem;
    /// <summary>Gets the new original application object, or null.</summary>
    public object? NewItem { get; } = newItem;
}
