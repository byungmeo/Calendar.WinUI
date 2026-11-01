# Calendar.WinUI

An extensible month calendar control for WinUI 3.

Display all-day, timed, and multi-day events with automatic five/six-week layouts, overflow indicators, custom templates, and independent date/event selection. Your application owns navigation, editing, storage, and service integration.

**Status:** Early development. Month view only. Supports .NET 8, 9, and 10 applications.

The `Byungmeo.Calendar.WinUI` NuGet package includes the control and Core APIs.

## Documentation

- [Getting Started](https://byungmeo.github.io/Calendar.WinUI/docs/getting-started.html) — package setup and first calendar.
- [Docs and API Reference](https://byungmeo.github.io/Calendar.WinUI/) — guides and automatically generated API documentation.

## Basic usage

Add the control to your WinUI XAML:

```xml
<calendar:Calendar
    xmlns:calendar="using:Calendar.WinUI.Controls"
    x:Name="Schedule" />
```

Supply events from your application:

```csharp
using Calendar.Core.Events;

Schedule.EventsSource = new[]
{
    new CalendarEvent("Meeting", CalendarEventData.AllDay(new DateOnly(2026, 11, 1)))
};
```

See [Calendar.Example](https://github.com/byungmeo/Calendar.WinUI/tree/master/samples/Calendar.Example) for a runnable application that consumes the NuGet package.

To build or contribute to this repository, see the [development guide](https://github.com/byungmeo/Calendar.WinUI/blob/master/docs/development.md).
