# Introduction

Calendar.WinUI is a month calendar control for WinUI 3. It displays dates and events and passes user interactions to your application. Your application decides how events are loaded, stored, and edited.

## Features

- A month grid with an automatic five or six weeks and configurable first weekday.
- All-day, timed, floating, and multi-day events.
- Overflow indicators that return all events on the selected date.
- Independent date and event selection, invocation events, and arrow-key navigation.
- Adapters for application models and templates for events, dates, and weekday headers.
- Per-control language and time-zone settings with system defaults and built-in English and Korean strings.

## Application responsibilities

The control does not include a month title, navigation buttons, or a Today button. Build these using <xref:Calendar.WinUI.Controls.Calendar.DisplayDate> and the exposed date ranges.

Your application connects dialogs, popups, and editing screens to calendar events. The control does not store data or contact external calendar services. Recurrence expansion, authentication, synchronization, and `.ics` parsing also belong to your application or adapter.

## Package

Install `Byungmeo.Calendar.WinUI` to get the control, interaction events, templates, and framework-independent Core APIs. Both assemblies are included in this single NuGet package. The namespaces remain `Calendar.WinUI` and `Calendar.Core`; Core is a separate source project, not a separate package dependency.

Only the month view is implemented. <xref:Calendar.Core.Views.CalendarDisplayUnit> contains `Month`; day and week views are not available yet.

Continue with [Getting Started](getting-started.md), or explore the [Calendar API](xref:Calendar.WinUI.Controls.Calendar).
