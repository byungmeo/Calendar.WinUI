#if CALENDAR_PACKAGE_CHECKS
using Calendar.Core.Events;
using Calendar.WinUI.Interaction;
using Calendar.WinUI.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Calendar.Example;

public sealed partial class MainWindow
{
    private int _dateInvocations;
    private int _eventInvocations;
    private int _eventSelectionChanges;
    private readonly StackPanel StatusPanel = new() { Spacing = 4 };
    private readonly TextBlock Status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock EventSelectionStatus = new();
    private readonly TextBlock RangeStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ToggleSwitch EventInvocationToggle = new() { IsOn = true };

    private void AddRegressionEvents(DateOnly month)
    {
        _events.Add(new CalendarEvent("Design and development", CalendarEventData.AllDay(month.AddDays(4), month.AddDays(8))));
        _events.Add(new CalendarEvent("Multi-day event: into next month", CalendarEventData.AllDay(month.AddDays(25), month.AddMonths(1).AddDays(3))));
        _events.Add(new CalendarEvent("Release preparation", CalendarEventData.AllDay(month.AddDays(27))));
        for (var i = 0; i < 9; i++)
            _events.Add(new CalendarEvent($"Timed event {i + 1}", CalendarEventData.Floating(
                month.AddDays(27).ToDateTime(new TimeOnly(9 + i, 0)), month.AddDays(27).ToDateTime(new TimeOnly(9 + i, 30)))));
        _events.Add(new WorkItem("Custom application model", month.AddDays(14), month.AddDays(16)));
        _events.Add(new CalendarEvent("UTC event (23:30Z)", CalendarEventData.Timed(
            new DateTimeOffset(month.AddDays(19).ToDateTime(new TimeOnly(23, 30)), TimeSpan.Zero))));
    }

    private void InitializeDiagnostics()
    {
        AutomationProperties.SetAutomationId(Status, "Status");
        AutomationProperties.SetAutomationId(EventSelectionStatus, "EventSelectionStatus");
        AutomationProperties.SetAutomationId(RangeStatus, "RangeStatus");
        StatusPanel.Children.Add(Status);
        StatusPanel.Children.Add(EventSelectionStatus);
        StatusPanel.Children.Add(RangeStatus);
        Grid.SetRow(StatusPanel, Root.RowDefinitions.Count);
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.Children.Add(StatusPanel);
        AutomationProperties.SetAutomationId(EventInvocationToggle, "EventInvocation");
        EventInvocationToggle.Toggled += (_, _) => Board.IsEventInvocationEnabled = EventInvocationToggle.IsOn;
        DisplaySettingsPanel.Children.Add(EventInvocationToggle);
        Board.DateInvoked += Date_Invoked;
        Board.SelectedDateChanged += DateSelection_Changed;
        Board.EventInvoked += Event_Invoked;
        Board.SelectedEventChanged += EventSelection_Changed;
        Board.MoreInvoked += More_Invoked;
        Board.VisibleRangeChanged += (_, _) => UpdateRangeStatus();
        RefreshDiagnostics();
    }

    partial void ResetDiagnostics()
    {
        _dateInvocations = _eventInvocations = _eventSelectionChanges = 0;
        Status.Text = Text("Reset");
        UpdateEventSelectionStatus();
    }

    partial void EventAdded(DateOnly date) => Status.Text = Text("AddedStatus", date, _events.Count);

    partial void TemplateActionInvoked(object sender)
    {
        _nestedInvocations++;
        var context = (sender as FrameworkElement)?.DataContext as CalendarEventContext;
        Status.Text = Text("TemplateStatus", context?.Title, _dateInvocations, _eventInvocations);
    }

    partial void RefreshDiagnostics()
    {
        EventInvocationToggle.Header = Text("EventInvocation");
        EventInvocationToggle.OnContent = Text("On");
        EventInvocationToggle.OffContent = Text("Off");
        Status.Text = Text("InitialStatus");
        UpdateEventSelectionStatus();
        UpdateRangeStatus();
    }

    private void Date_Invoked(object? sender, CalendarDateEventArgs e) =>
        Status.Text = Text("DateStatus", e.Date, e.IsOutsidePrimaryRange, ++_dateInvocations, _eventInvocations);

    private void DateSelection_Changed(object? sender, CalendarSelectionChangedEventArgs e) =>
        Status.Text = Text("DateSelectionStatus", e.NewDate?.ToString("yyyy-MM-dd", _uiCulture) ?? Text("None"), _dateInvocations, _eventInvocations);

    private void Event_Invoked(object? sender, CalendarEventInvokedEventArgs e) =>
        Status.Text = Text("EventStatus", e.Occurrence.DisplayText, e.Date, e.IsOutsidePrimaryRange, _dateInvocations, ++_eventInvocations);

    private void EventSelection_Changed(object? sender, CalendarEventSelectionChangedEventArgs e)
    {
        _eventSelectionChanges++;
        UpdateEventSelectionStatus();
    }

    private void UpdateEventSelectionStatus() =>
        EventSelectionStatus.Text = Text("SelectedEventStatus", Board.SelectedEvent?.DisplayText ?? Text("None"), _eventSelectionChanges);

    private void More_Invoked(object? sender, CalendarMoreInvokedEventArgs e) =>
        Status.Text = Text("MoreStatus", e.Date, e.Events.Count, e.IsOutsidePrimaryRange, _dateInvocations, _eventInvocations);

    private void UpdateRangeStatus() =>
        RangeStatus.Text = Text("Range", Board.PrimaryRange.Start, Board.PrimaryRange.EndExclusive, Board.VisibleRange.Start, Board.VisibleRange.EndExclusive);
}
#endif
