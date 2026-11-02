using System.Collections.Specialized;
using System.ComponentModel;
using Calendar.Core.Events;

namespace Calendar.WinUI.Controls;

public sealed partial class Calendar
{
    private bool _dataDirty = true;
    private bool _allItemsDirty = true;
    private bool _dataPending;
    private int _deferRefresh;
    private readonly HashSet<object> _dirtyItems = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, ProjectionEntry> _projectionCache = new(ReferenceEqualityComparer.Instance);
    private List<CalendarOccurrence> _pendingOccurrences = new();
    private readonly HashSet<object> _occurrenceKeys = new();
    private bool _subscriptionsActive;
    private readonly record struct ProjectionEntry(CalendarOccurrence Occurrence, TimeZoneInfo Zone);

    private sealed class RefreshScope(Calendar calendar) : IDisposable
    {
        private Calendar? _calendar = calendar;
        public void Dispose()
        {
            if (_calendar is not { } owner) return;
            owner.VerifyDataAccess();
            _calendar = null;
            if (--owner._deferRefresh == 0 && owner._dataDirty) owner.ProcessData();
        }
    }

    private void VerifyDataAccess()
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Change calendar data on the UI thread.");
    }

    private void InvalidateData(bool allItems = false, object? item = null)
    {
        VerifyDataAccess();
        _dataDirty = true;
        _allItemsDirty |= allItems;
        if (item is not null) _dirtyItems.Add(item);
        if (_dataPending || _deferRefresh != 0) return;
        _dataPending = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _dataPending = false;
            if (_dataDirty && _deferRefresh == 0) ProcessData();
        })) _dataPending = false;
    }

    private void ProcessData()
    {
        // Validate into reusable staging buffers. Invalid input never replaces the last valid projection.
        var projected = _pendingOccurrences;
        var keys = _occurrenceKeys;
        var adapter = EventAdapter;
        var zone = DisplayTimeZone;
        var membershipChanged = !_subscriptionsActive;
        var keyCount = 0;
        try
        {
            if (EventsSource is { } source)
            {
                var index = 0;
                foreach (var item in source)
                {
                    if (item is null) throw new ArgumentException($"EventsSource contains null at index {index}.");
                    if (index >= _occurrences.Count || !ReferenceEquals(item, _occurrences[index].Source)) membershipChanged = true;
                    var cached = _projectionCache.TryGetValue(item, out var entry);
                    CalendarOccurrence occurrence;
                    if (cached && !_allItemsDirty && !_dirtyItems.Contains(item))
                    {
                        var old = entry.Occurrence;
                        occurrence = old.SourceIndex == index && entry.Zone.Equals(zone) ? old
                            : CalendarOccurrence.Project(item, old.Data, old.DisplayText, index, zone, old.Key);
                    }
                    else
                    {
                        var key = (adapter as ICalendarEventKeyProvider)?.GetKey(item);
                        occurrence = CalendarOccurrence.Project(item, adapter.GetEvent(item), adapter.GetDisplayText(item), index, zone, key);
                    }
                    if (occurrence.Key is { } identity && !keys.Add(identity))
                        throw new ArgumentException($"EventsSource contains a duplicate occurrence key at index {index}. Keys must identify individual occurrences.");
                    projected.Add(occurrence);
                    index++;
                }
            }
            keyCount = keys.Count;
        }
        catch
        {
            projected.Clear();
            throw;
        }
        finally { keys.Clear(); }
        membershipChanged |= projected.Count != _occurrences.Count;
        // No user callbacks remain in this commit: keys and adapters were validated above.
        _projectionCache.Clear();
        foreach (var occurrence in projected) _projectionCache[occurrence.Source] = new(occurrence, zone);
        var previousOccurrences = _occurrences as List<CalendarOccurrence>;
        _occurrences = projected;
        _pendingOccurrences = previousOccurrences ?? new();
        _pendingOccurrences.Clear();
        _dirtyItems.Clear();
        _dataDirty = _allItemsDirty = false;
        if (membershipChanged) UpdateSubscriptions(projected);
        TrimDataBuffers(projected.Count, keyCount);
        ResolveSelection();
        _eventsDirty = true;
        QueueRender();
    }

    private void UpdateSubscriptions(IEnumerable<CalendarOccurrence> projected)
    {
        if (!_loaded) return;
        var collection = EventsSource as INotifyCollectionChanged;
        if (!ReferenceEquals(_collection, collection))
        {
            if (_collection is not null) _collection.CollectionChanged -= CollectionChanged;
            _collection = collection;
            if (_collection is not null) _collection.CollectionChanged += CollectionChanged;
        }
        _observed.RemoveWhere(item =>
        {
            if (_projectionCache.ContainsKey(item)) return false;
            item.PropertyChanged -= ItemChanged;
            return true;
        });
        foreach (var occurrence in projected)
            if (occurrence.Source is INotifyPropertyChanged item && _observed.Add(item)) item.PropertyChanged += ItemChanged;
        _subscriptionsActive = true;
    }

    private void TrimDataBuffers(int count, int keyCount)
    {
        // Release unusually large backing arrays after a substantial data reduction, not on ordinary edits.
        if (ShouldTrim(_projectionCache.EnsureCapacity(0), count)) _projectionCache.TrimExcess(count);
        if (ShouldTrim(_pendingOccurrences.Capacity, count)) _pendingOccurrences.Capacity = count;
        if (_occurrences is List<CalendarOccurrence> current && ShouldTrim(current.Capacity, count)) current.Capacity = count;
        if (ShouldTrim(_occurrenceKeys.EnsureCapacity(0), keyCount)) _occurrenceKeys.TrimExcess();
        if (ShouldTrim(_observed.EnsureCapacity(0), count)) _observed.TrimExcess();
        if (ShouldTrim(_dirtyItems.EnsureCapacity(0), count)) _dirtyItems.TrimExcess();
    }
    private static bool ShouldTrim(int capacity, int count) => capacity >= 1024 && count < capacity / 4;

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateData();
    private void ItemChanged(object? sender, PropertyChangedEventArgs e) => InvalidateData(item: sender);
}
