# Calendar.Core

Framework-independent calendar event models, time-zone projection, and month layout for .NET 8 and later. This project is built as a separate assembly and distributed inside the `Byungmeo.Calendar.WinUI` NuGet package.

- Date-only, absolute, and floating event semantics with exclusive ends.
- Adapters for application models that preserve original source objects.
- Automatic five/six-week grids, multi-day segments, ordering, and overflow counts.

```csharp
using Calendar.Core.Events;
using Calendar.Core.Views.Month;

var date = new DateOnly(2026, 11, 1);
var source = new CalendarEvent("Meeting", CalendarEventData.AllDay(date));
var occurrence = CalendarOccurrence.Project(
    source, source.Data, source.Title, 0, TimeZoneInfo.Local);

var grid = MonthGrid.Create(date, DayOfWeek.Monday);
var layout = MonthLayoutEngine.Build(grid, new[] { occurrence }, rowCapacity: 3);
```

Install `Byungmeo.Calendar.WinUI` to use these APIs. There is no standalone `Calendar.Core` NuGet package.

[API Reference](https://byungmeo.github.io/Calendar.WinUI/api/Calendar.Core.Events.CalendarEventData.html) · [Repository](https://github.com/byungmeo/Calendar.WinUI)
