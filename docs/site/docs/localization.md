# Language and Time Zones

Both settings default to the system. They apply to each calendar instance without changing the application's global culture. This documentation website is English-only; its language is independent of the control's runtime setting.

## Display language

Set <xref:Calendar.WinUI.Controls.Calendar.DisplayLanguage> to a culture tag. Built-in control strings are available in English and Korean; date and weekday formatting follows the selected culture.

```csharp
Schedule.DisplayLanguage = "ko-KR";
Schedule.DisplayLanguage = "en-US";
Schedule.DisplayLanguage = null; // Use the system UI culture.
```

Null or an empty string uses `CultureInfo.CurrentUICulture`. Today, overflow indicators, selection announcements, and date and weekday text are refreshed. Application-provided event titles are not translated.

Explicit `TodayText` and `MoreTextFormat` values take precedence. `FirstDayOfWeek` is independent of display language.

## Display time zone

<xref:Calendar.WinUI.Controls.Calendar.DisplayTimeZone> determines date-cell membership for absolute events and the date considered today.

```csharp
Schedule.DisplayTimeZone = TimeZoneInfo.Utc;
Schedule.DisplayTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");

// Restore the system time zone.
Schedule.ClearValue(global::Calendar.WinUI.Controls.Calendar.DisplayTimeZoneProperty);
```

An unset value uses `TimeZoneInfo.Local`. An event at October 20, 23:30 UTC appears on October 21 in UTC+09. Its original instant remains unchanged.

All-day dates and floating times do not move with time-zone changes. See <xref:Calendar.Core.Events.CalendarTimeKind> for the supported time semantics.

Timed-event sorting currently uses the displayed wall-clock value, `LocalStart`. During a repeated hour at the end of daylight saving time, this can differ from UTC instant order.

## Dates versus instants

`DisplayDate`, `SelectedDate`, and interaction argument `Date` values are date-only transports. Do not convert them as UTC instants merely because their type is `DateTimeOffset`. In contrast, `Start` and `End` supplied to `CalendarEventData.Timed` represent actual instants.

Use C# helpers for calendar-date operations:

```csharp
Schedule.DisplayDateOnly = Schedule.Today;
DateOnly? selectedDate = Schedule.SelectedDateOnly;
```

`Today` is computed in `DisplayTimeZone` on each access. `DisplayDateOnly` and `SelectedDateOnly` wrap the existing dependency properties; their values represent calendar components without time-zone conversion. They are C# convenience properties, not additional bindable dependency properties.

`TimeZoneId` metadata does not resolve original TZID or custom `VTIMEZONE` definitions. The application or adapter must interpret those definitions before supplying occurrences.

## Example application

`Calendar.Example` offers System, Korean, and English language choices and a UTC-offset selector. System time follows the local time zone's daylight-saving rules; fixed UTC offsets do not apply daylight saving. Its Today button navigates using the selected display time zone.
