using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Calendar.Core.Events;

/// <summary>Optional, observable convenience model. Custom application models need not inherit it.</summary>
public sealed class CalendarEvent : INotifyPropertyChanged
{
    private string _title;
    private CalendarEventData _data;
    /// <summary>Creates an observable event with a title and occurrence data.</summary>
    public CalendarEvent(string title, CalendarEventData data)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }
    /// <summary>Gets or sets the display title.</summary>
    public string Title { get => _title; set => Set(ref _title, value ?? string.Empty); }
    /// <summary>Gets or sets the occurrence data.</summary>
    public CalendarEventData Data { get => _data; set => Set(ref _data, value ?? throw new ArgumentNullException(nameof(value))); }
    /// <summary>Occurs when an observable property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    /// <inheritdoc/>
    public override string ToString() => Title;
}

/// <summary>Maps the caller's object to a single occurrence without reflection or service dependencies.</summary>
public interface ICalendarEventAdapter
{
    /// <summary>Returns the occurrence data represented by an application object.</summary>
    CalendarEventData GetEvent(object source);
    /// <summary>Returns the display text for an application object.</summary>
    string GetDisplayText(object source);
}

/// <summary>Adapts CalendarEvent and CalendarEventData objects without reflection.</summary>
public sealed class DefaultCalendarEventAdapter : ICalendarEventAdapter
{
    /// <summary>Gets the shared default adapter.</summary>
    public static DefaultCalendarEventAdapter Instance { get; } = new();
    /// <summary>Returns the occurrence data represented by an application object.</summary>
    public CalendarEventData GetEvent(object source) => source switch
    {
        CalendarEvent item => item.Data,
        CalendarEventData data => data,
        _ => throw new ArgumentException("Provide an EventAdapter for your application model.", nameof(source))
    };
    /// <summary>Returns the display text for an application object.</summary>
    public string GetDisplayText(object source) => source is CalendarEvent item ? item.Title : string.Empty;
}
