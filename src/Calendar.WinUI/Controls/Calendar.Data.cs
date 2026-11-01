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
    private sealed record ProjectionEntry(CalendarOccurrence Occurrence, TimeZoneInfo Zone);

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
        var projected = new List<CalendarOccurrence>();
        var cache = new Dictionary<object, ProjectionEntry>(ReferenceEqualityComparer.Instance);
        var keys = new HashSet<object>();
        var adapter = EventAdapter;
        var zone = DisplayTimeZone;
        if (EventsSource is { } source)
        {
            var index = 0;
            foreach (var item in source)
            {
                if (item is null) throw new ArgumentException($"EventsSource contains null at index {index}.");
                _projectionCache.TryGetValue(item, out var entry);
                CalendarOccurrence occurrence;
                if (entry is not null && !_allItemsDirty && !_dirtyItems.Contains(item))
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
                cache[item] = new(occurrence, zone);
                index++;
            }
        }
        _occurrences = projected.AsReadOnly();
        _projectionCache.Clear();
        foreach (var pair in cache) _projectionCache.Add(pair.Key, pair.Value);
        _dirtyItems.Clear();
        _dataDirty = _allItemsDirty = false;
        UpdateSubscriptions(projected);
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
        var next = new HashSet<INotifyPropertyChanged>(projected.Select(p => p.Source).OfType<INotifyPropertyChanged>(), ReferenceEqualityComparer.Instance);
        foreach (var item in _observed.Where(item => !next.Contains(item)).ToArray())
        {
            item.PropertyChanged -= ItemChanged;
            _observed.Remove(item);
        }
        foreach (var item in next)
            if (_observed.Add(item)) item.PropertyChanged += ItemChanged;
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateData();
    private void ItemChanged(object? sender, PropertyChangedEventArgs e) => InvalidateData(item: sender);
}
