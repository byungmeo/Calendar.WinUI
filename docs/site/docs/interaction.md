# Selection and Input

## Selection and invocation

Selection is state retained by the control. Invocation is a notification that a user activated an element by pointer or keyboard.

| Event | Trigger | Main payload |
| --- | --- | --- |
| `DateInvoked` | Date activation, including repeated activation | Date, adjacent-month flag, and anchor |
| `SelectedDateChanged` | The selected date changes | Previous and new dates |
| `EventInvoked` | An event is activated | Date, occurrence, and original application object |
| `SelectedEventChanged` | The selected occurrence changes | Previous and new occurrences |
| `SelectedItemChanged` | The original selected model changes | Previous and new application objects |
| `MoreInvoked` | An overflow indicator is activated | Date and all occurrences on that date |

Event argument types are documented in [Calendar.WinUI.Interaction](xref:Calendar.WinUI.Interaction).

## Configure selection

<xref:Calendar.WinUI.Controls.Calendar.SelectionMode> defaults to `Single`. With `None`, dates remain invokable without updating selection.

```csharp
Schedule.SelectionMode = Calendar.Core.Views.CalendarSelectionMode.None;
Schedule.IsEventSelectionEnabled = false;
```

`IsEventSelectionEnabled` and `IsEventInvocationEnabled` both default to `true` and are independent:

| Selection | Invocation | Event activation |
| --- | --- | --- |
| Enabled | Enabled | Selects the event and raises `EventInvoked` |
| Disabled | Enabled | Raises `EventInvoked` without selecting |
| Enabled | Disabled | Selects without raising `EventInvoked` |
| Disabled | Disabled | Displays the event without a parent activation action |

To retain the earlier display-only behavior, set **both** properties to false. Independent controls inside an event template stay enabled. Date and overflow interactions remain available in all four configurations.

## Select an application model

```csharp
Schedule.SelectedItem = appointment;
var occurrence = Schedule.SelectedOccurrence;
Schedule.SelectedItem = null; // Clear both model and occurrence selection.
```

<xref:Calendar.WinUI.Controls.Calendar.SelectedItem> is bindable and accepts an original source object. An item assigned before its data is available remains pending until a refresh resolves it. Editing a selected model's event data keeps selection. Removing a resolved selected item clears selection.

Stable adapter keys can preserve selection across replacement by a new model object. `SelectedItem` then changes to the current source object and raises `SelectedItemChanged`. `SelectedEventChanged` reports a logical identity change, rather than title, date, time-zone, or same-key model replacement. See [Events and Data](events.md) for keys and change notifications.

The existing bindable <xref:Calendar.WinUI.Controls.Calendar.SelectedEvent> remains supported and synchronized with `SelectedItem`. `SelectedOccurrence` is a read-only C# convenience getter for that property; use `SelectedEvent` for XAML bindings to the projection.

All visible segments of the selected occurrence receive the selection outline. Date and event selection are independent: invoking an event or overflow indicator does not select its date. Disabling user selection hides its outline and preserves any existing programmatic selection.

## Overflow indicators

When space runs out, the last event row displays the hidden count. <xref:Calendar.WinUI.Interaction.CalendarMoreInvokedEventArgs.Events> includes **every occurrence on that date, including visible ones**.

```csharp
Schedule.MoreInvoked += (_, e) =>
{
    var date = e.DateOnly;
    var allEventsOnDate = e.Events;
    // Build the application's event list or popup.
};
```

The control does not open an overflow popup or provide a default event tooltip.

## Date-only values

The `DateTimeOffset` values in date selection and invocation APIs are **date-only transport values** for WinUI. Use their year, month, and day; do not convert them as UTC instants. `IsOutsidePrimaryRange` indicates whether the activated cell belonged to an adjacent month. Invoking an adjacent date does not navigate automatically.

For C# callers, use `DisplayDateOnly`, `SelectedDateOnly`, and the interaction argument's `DateOnly` getter to avoid transport conversions. These helpers wrap the existing properties; they do not introduce additional dependency properties.

## Keyboard navigation

With date selection enabled, Left and Right immediately select the previous or next day; Up and Down move one week from the current selected date. Navigation stops at the visible range instead of changing months. Arrow keys raise `SelectedDateChanged`, not `DateInvoked`. Enter and Space invoke the focused date.

## Connect editing UI

Use `Anchor` as a positioning element for application-owned popups. The application decides whether to edit, delete, or save an event. An event's `Source` is the original object passed to the adapter.
