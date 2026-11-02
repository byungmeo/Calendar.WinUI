# Getting Started

## Requirements

Start with a WinUI 3 application using Windows App SDK 2.0.1 or later. The package targets `net8.0-windows10.0.19041.0`, with consuming applications tested on .NET 8, 9, and 10. Building this repository requires the SDK specified in `global.json`.

The current package version is `0.2.0`. These instructions use a local package feed and do not assume publication to NuGet.org.

## Install the package

Build the package from the repository root:

```powershell
./scripts/build.ps1 -PackagesOnly
```

Add the local feed to your application's `.csproj`, keeping its existing NuGet sources:

```xml
<PropertyGroup>
  <RestoreAdditionalProjectSources>C:\Workspace\Calendar.WinUI\artifacts\packages</RestoreAdditionalProjectSources>
</PropertyGroup>
```

From the application project directory, install `Byungmeo.Calendar.WinUI`. The package contains both the UI and Core assemblies; no separate Core package is needed. The namespaces remain `Calendar.WinUI` and `Calendar.Core`.

```powershell
dotnet add package Byungmeo.Calendar.WinUI --version 0.2.0
```

## Add the control

Declare the XAML namespace and place the calendar in a bounded layout area. This example uses a `Window`:

```xml
<Window
    x:Class="MyApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:calendar="using:Calendar.WinUI.Controls">
    <Grid Padding="24">
        <calendar:Calendar x:Name="Schedule" />
    </Grid>
</Window>
```

## Supply events

```csharp
using System.Collections.ObjectModel;
using Calendar.Core.Events;

public MainWindow()
{
    InitializeComponent();
    Schedule.DisplayDateOnly = new DateOnly(2026, 10, 1);
    Schedule.EventsSource = new ObservableCollection<CalendarEvent>
    {
        new("Business trip", CalendarEventData.AllDay(
            new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 15))),
        new("Meeting", CalendarEventData.Timed(
            new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(9)),
            new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.FromHours(9))))
    };
}
```

All-day intervals have an exclusive end date: the trip above occupies October 12 through 14. Absolute timed events are projected into <xref:Calendar.WinUI.Controls.Calendar.DisplayTimeZone> before being assigned to date cells.

## Handle interactions

```csharp
Schedule.DateInvoked += (_, e) =>
{
    var selectedDate = e.DateOnly;
    // Open the application's create-event UI for selectedDate.
};

Schedule.EventInvoked += (_, e) =>
{
    var originalModel = e.Source;
    // Open the application's details UI for originalModel.
};
```

Date and event selection are enabled by default. See [Selection and Input](interaction.md) for the distinction between selection changes and invocation.

## Change the displayed month

Update `DisplayDate` from your own navigation button handlers:

```csharp
Schedule.DisplayDate = Schedule.DisplayDate.AddMonths(1);
```

Your application displays the title and observes <xref:Calendar.WinUI.Controls.Calendar.VisibleRangeChanged> for range changes. Subscribe before the calendar loads to receive the initial range once, then subsequent changes:

```csharp
Schedule.VisibleRangeChanged += (_, e) =>
{
    LoadEvents(e.NewRange.VisibleRange); // Application-owned data loading.
};
```

No separate initial-load handler is required. Reading `PrimaryRange` or `VisibleRange` before load is still supported. The repository's `samples/Calendar.Example` project contains a complete runnable example that consumes the NuGet package without project references. Run `./scripts/build.ps1` from the repository root to create the local package and build that example.
