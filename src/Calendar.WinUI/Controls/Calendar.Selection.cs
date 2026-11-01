using Calendar.Core.Events;
using Microsoft.UI.Xaml;

namespace Calendar.WinUI.Controls;

public sealed partial class Calendar
{
    private bool _synchronizingSelection;

    private static void ItemSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var calendar = (Calendar)sender;
        if (calendar._synchronizingSelection) return;
        // The source, adapter, and requested selection may all be changing in the same initialization.
        var occurrence = calendar._dataDirty ? null : calendar.FindOccurrence(e.NewValue);
        calendar.SynchronizeSelection(calendar.SelectedEvent, occurrence, e.OldValue, occurrence?.Source ?? e.NewValue);
    }

    private CalendarOccurrence? FindOccurrence(object? item, CalendarOccurrence? previous = null)
    {
        if (item is null) return null;
        var byReference = _occurrences.FirstOrDefault(occurrence => ReferenceEquals(occurrence.Source, item));
        if (byReference is not null) return byReference;
        var key = previous?.Key ?? (EventAdapter as ICalendarEventKeyProvider)?.GetKey(item);
        return key is null ? null : _occurrences.FirstOrDefault(occurrence => Equals(occurrence.Key, key));
    }

    private void ResolveSelection()
    {
        if (SelectedItem is not { } item) return;
        var previous = SelectedEvent;
        var next = FindOccurrence(item, previous);
        // A pending item may be assigned before its data arrives. A resolved item removed from data clears selection.
        var source = next?.Source ?? (previous is null ? item : null);
        SynchronizeSelection(previous, next, item, source);
    }

    private void SynchronizeSelection(CalendarOccurrence? previous, CalendarOccurrence? next, object? oldItem, object? newItem)
    {
        _synchronizingSelection = true;
        try
        {
            if (!ReferenceEquals(SelectedEvent, next)) SetValue(SelectedEventProperty, next);
            if (!ReferenceEquals(SelectedItem, newItem)) SetValue(SelectedItemProperty, newItem);
        }
        finally { _synchronizingSelection = false; }
        RefreshEventStyles(previous, next);
        if (!SameOccurrence(previous, next)) SelectedEventChanged?.Invoke(this, new(previous, next));
        if (!ReferenceEquals(oldItem, newItem)) SelectedItemChanged?.Invoke(this, new(oldItem, newItem));
    }
}
