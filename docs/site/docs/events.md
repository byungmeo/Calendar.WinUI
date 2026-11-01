# Events and Data

## Built-in models

<xref:Calendar.Core.Events.CalendarEvent> is an optional convenience model containing a title and occurrence data. It implements `INotifyPropertyChanged`; application models do not need to inherit from it.

<xref:Calendar.Core.Events.CalendarEventData> represents one occurrence:

| Kind | Factory | Meaning |
| --- | --- | --- |
| All-day | `AllDay(start, endExclusive)` | A date interval unaffected by time-zone changes |
| Absolute | `Timed(start, end)` | An instant or interval with an explicit UTC offset |
| Floating | `Floating(start, end)` | Wall-clock time preserved across display time zones |

An omitted all-day end produces a one-day event. An omitted timed or floating end represents a point event. An explicitly supplied end must be later than the start. Floating values must have `DateTimeKind.Unspecified`.

## Multi-day events

One occurrence can produce multiple visible segments when it crosses week boundaries. Invoking any segment returns the original <xref:Calendar.WinUI.Interaction.CalendarEventInvokedEventArgs.Occurrence> and application `Source`.

All-day events are placed first, preserving application input order. Timed events follow in displayed start-time order, with application input order breaking ties.

## Adapt application models

Use <xref:Calendar.Core.Events.CalendarEventAdapter> to map your own models without casts or an additional class:

```csharp
record Appointment(string Id, string Title, DateOnly Start, DateOnly EndExclusive);

Schedule.EventsSource = appointments;
Schedule.EventAdapter = CalendarEventAdapter.Create<Appointment>(
    data: item => CalendarEventData.AllDay(item.Start, item.EndExclusive),
    text: item => item.Title,
    key: item => item.Id);
```

Source and adapter assignments in the same synchronous UI-thread initialization are processed together on the dispatcher. There is no requirement to assign the adapter first. An explicitly called `RefreshEvents()` processes the current combination synchronously, so both must be ready before that call.

The optional key preserves selection when a server response replaces a model object. Keys must be stable and unique per occurrence. For recurring data, use a composite key such as `(Uid, RecurrenceId)`, rather than a series UID alone. Duplicate non-null keys are rejected. Omitting the key uses source reference identity; record value equality is not used as a substitute.

Implement <xref:Calendar.Core.Events.ICalendarEventAdapter> directly for more complex mappings. Existing adapters remain supported; they can optionally implement <xref:Calendar.Core.Events.ICalendarEventKeyProvider>.

The default adapter accepts `CalendarEvent` and `CalendarEventData`. A bare `CalendarEventData` has an empty display title.

## Notify changes

The control observes `INotifyCollectionChanged` collections, such as `ObservableCollection`, and item-level `INotifyPropertyChanged`. Automatic changes are coalesced on the UI dispatcher. Unchanged models reuse their projection and subscriptions; changes are visible after the dispatcher processes them. After modifying a model without notifications, call <xref:Calendar.WinUI.Controls.Calendar.RefreshEvents>:

```csharp
Schedule.RefreshEvents();
```

Change data and refresh the control on the UI thread. Do not include null items in `EventsSource`.

## Batch updates

Use <xref:Calendar.WinUI.Controls.Calendar.DeferRefresh> to process a group of changes once while keeping the collection connected:

```csharp
using (Schedule.DeferRefresh())
{
    foreach (var item in incomingEvents)
        events.Add(item);
}
```

Scopes can be nested. Only disposal of the outermost scope processes pending data. Disposal is idempotent and must occur on the UI thread. Do not hold a scope across `await`; fetch data first, then enter the scope to apply it. `RefreshEvents()` inside a scope also waits for its disposal.

## External calendar data

Map provider models through an adapter. `Uid`, `RecurrenceId`, and `TimeZoneId` are optional original metadata; the control does not expand recurrences or resolve TZID values. Supply individual occurrences already interpreted by the provider or application.

The model distinguishes iCalendar event semantics such as all-day dates, point events, floating time, and exclusive ends. It is not a complete RFC 5545 parser or a calendar-service client.
