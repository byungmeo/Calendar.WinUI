# Month Layout

## Date ranges

<xref:Calendar.WinUI.Controls.Calendar.DisplayDate> determines the displayed month. `PrimaryRange` covers that month; `VisibleRange` includes adjacent-month cells. Both ranges have exclusive ends.

The grid contains 35 or 42 dates, with a minimum of five weeks. There is no fixed-six-week option.

```csharp
Schedule.FirstDayOfWeek = DayOfWeek.Monday;
var primaryMonth = Schedule.PrimaryRange;
var visibleDates = Schedule.VisibleRange;
```

<xref:Calendar.WinUI.Controls.Calendar.VisibleRangeChanged> reports the initial range once on load, followed by changes to the loaded view. The application supplies the month title and navigation buttons.

## Date appearance

Dates in the primary month display their day number. Adjacent dates include the month, such as `9/27`. Backgrounds distinguish past dates from today and future dates in the display time zone. Today has a circular highlight and label; selected dates have a separate outline.

## Event capacity

Available week height and event-row height determine how many rows fit. When events overflow, the last row is reserved for overflow indicators. Empty lanes can remain to preserve event priority and multi-day continuity.

Configure row sizes through <xref:Calendar.WinUI.Controls.MonthViewOptions>. Values are in DIPs and must be finite and at least 20.

```xml
<calendar:Calendar>
    <calendar:Calendar.MonthOptions>
        <calendar:MonthViewOptions
            DateHeaderHeight="28"
            EventRowHeight="24" />
    </calendar:Calendar.MonthOptions>
</calendar:Calendar>
```

Place the control in a bounded Grid area. Individual events do not have independent row heights.

Application-assigned `MinWidth` and `MinHeight` values and bindings are preserved during rendering. The default style supplies a minimum width of 280 DIPs; internal week rows reserve space for the date header and one event row without rewriting the public minimum-size properties.

## Day and week views

<xref:Calendar.Core.Views.CalendarDisplayUnit> currently supports only `Month`. Day and week views and hourly time-axis layouts are not implemented.
