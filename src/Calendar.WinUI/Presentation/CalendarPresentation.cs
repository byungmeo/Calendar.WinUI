using Calendar.Core.Events;
using Calendar.Core.Dates;
using System.Globalization;
using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace Calendar.WinUI.Presentation;

/// <summary>Bindable content for a date header template.</summary>
[WinRT.GeneratedBindableCustomProperty]
public sealed partial class CalendarDateContext : INotifyPropertyChanged
{
    private bool _isSelected;
    /// <summary>Occurs when selection state changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void SetSelected(bool selected)
    {
        if (_isSelected == selected) return;
        _isSelected = selected;
        PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
    }
    /// <summary>Gets the date-only transport value for this cell. The UTC offset is not significant.</summary>
    public required DateTimeOffset Date { get; init; }
    internal CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;
    /// <summary>Gets the localized day text, including the month for an adjacent-month cell.</summary>
    public required string DayText { get; init; }
    /// <summary>Gets whether this cell represents today in the display time zone.</summary>
    public bool IsToday { get; init; }
    /// <summary>Gets whether the represented date or occurrence is selected.</summary>
    public bool IsSelected { get => _isSelected; init => _isSelected = value; }
    /// <summary>Gets whether this date lies outside the displayed primary month.</summary>
    public bool IsOutsidePrimaryRange { get; init; }
    /// <summary>Gets visibility for the today indicator.</summary>
    public Visibility TodayVisibility => IsToday ? Visibility.Visible : Visibility.Collapsed;
    /// <summary>Gets visibility for the ordinary date text.</summary>
    public Visibility RegularDateVisibility => IsToday ? Visibility.Collapsed : Visibility.Visible;
    /// <summary>Gets the day number in the display culture.</summary>
    public string DayNumberText => Date.Day.ToString(Culture);
    /// <summary>Gets the adjacent-month prefix, or an empty string within the primary month.</summary>
    public string MonthPrefixText => IsOutsidePrimaryRange ? Date.Month.ToString(Culture) + "/" : string.Empty;
    /// <summary>Gets the localized or application-supplied today label.</summary>
    public required string TodayText { get; init; }
}

/// <summary>Bindable content for one visible event segment.</summary>
[WinRT.GeneratedBindableCustomProperty]
public sealed partial class CalendarEventContext : INotifyPropertyChanged
{
    private bool _isSelected;
    /// <summary>Gets whether the represented date or occurrence is selected.</summary>
    public bool IsSelected => _isSelected;
    /// <summary>Occurs when selection state changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void SetSelected(bool selected)
    {
        if (_isSelected == selected) return;
        _isSelected = selected;
        PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
    }
    /// <summary>Gets the original, unclipped occurrence.</summary>
    public required CalendarOccurrence Occurrence { get; init; }
    /// <summary>Gets the date interval occupied by this visible segment, with an exclusive end.</summary>
    public required DateRange DisplayedDates { get; init; }
    /// <summary>Gets the original application object.</summary>
    public object Source => Occurrence.Source;
    /// <summary>Gets the adapter-supplied display title.</summary>
    public string Title => Occurrence.DisplayText;
    /// <summary>Gets a stable example action identifier for this occurrence segment.</summary>
    public string ActionAutomationId => $"TemplateAction_{Occurrence.SourceIndex}_{DisplayedDates.Start:yyyy-MM-dd}";
    /// <summary>Gets whether the occurrence begins before this visible segment.</summary>
    public bool ContinuesBefore => Occurrence.Dates.Start < DisplayedDates.Start;
    /// <summary>Gets whether the occurrence continues after this visible segment.</summary>
    public bool ContinuesAfter => Occurrence.Dates.EndExclusive > DisplayedDates.EndExclusive;
    /// <summary>Gets the title with continuation markers for this segment.</summary>
    public string Label => $"{(ContinuesBefore ? "‹ " : "")}{Title}{(ContinuesAfter ? " ›" : "")}";
}

/// <summary>Bindable content for a weekday header template.</summary>
[WinRT.GeneratedBindableCustomProperty]
public sealed partial class CalendarDayOfWeekContext
{
    /// <summary>Gets the represented weekday.</summary>
    public DayOfWeek DayOfWeek { get; init; }
    /// <summary>Gets the weekday text in the display language.</summary>
    public required string Text { get; init; }
}
