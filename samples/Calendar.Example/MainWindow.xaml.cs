using System.Collections.ObjectModel;
using System.Globalization;
using Calendar.Core.Events;
using Calendar.Core.Views;
using Calendar.WinUI.Interaction;
using Calendar.WinUI.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Calendar.Example;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<object> _events = new();
    private int _dateInvocations;
    private int _eventInvocations;
    private int _eventSelectionChanges;
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1200, 1000));
        Board.EventAdapter = new ExampleAdapter();
        Board.DisplayDate = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        Board.FirstDayOfWeek = DayOfWeek.Sunday;
        Board.EventsSource = _events;
        Seed();
        InitializeSettings();
        UpdateTitle();
#if CALENDAR_PACKAGE_CHECKS
        InitializePackageChecks();
#endif
    }
    private void Seed()
    {
        using var refresh = Board.DeferRefresh();
        _events.Clear();
        _events.Add(new CalendarEvent("설계와 개발", CalendarEventData.AllDay(new(2026, 10, 5), new DateOnly(2026, 10, 9))));
        _events.Add(new CalendarEvent("기간 이벤트: 다음 달까지", CalendarEventData.AllDay(new(2026, 10, 26), new DateOnly(2026, 11, 4))));
        _events.Add(new CalendarEvent("출시 준비", CalendarEventData.AllDay(new(2026, 10, 28))));
        for (var i = 0; i < 9; i++)
            _events.Add(new CalendarEvent($"시간제 이벤트 {i + 1}", CalendarEventData.Floating(new(2026, 10, 28, 9 + i, 0, 0), new DateTime(2026, 10, 28, 9 + i, 30, 0))));
        _events.Add(new WorkItem("앱 고유 모델", new(2026, 10, 15), new(2026, 10, 17)));
        _events.Add(new CalendarEvent("UTC 시점 이벤트 (23:30Z)", CalendarEventData.Timed(new(2026, 10, 20, 23, 30, 0, TimeSpan.Zero))));
    }
    private void Previous_Click(object sender, RoutedEventArgs e) => Board.DisplayDate = Board.DisplayDate.AddMonths(-1);
    private void Next_Click(object sender, RoutedEventArgs e) => Board.DisplayDate = Board.DisplayDate.AddMonths(1);
    private void Today_Click(object sender, RoutedEventArgs e) => Board.DisplayDateOnly = Board.Today;
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Board.DisplayDate = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        _dateInvocations = 0; _eventInvocations = 0;
        Seed(); Status.Text = Text("Reset");
        _eventSelectionChanges = 0;
        UpdateEventSelectionStatus();
    }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var date = Board.SelectedDateOnly ?? Board.DisplayDateOnly;
        _events.Add(new CalendarEvent($"추가 이벤트 {_events.Count + 1}", CalendarEventData.AllDay(date)));
        Status.Text = Text("AddedStatus", date, _events.Count);
    }
    private void Template_Toggled(object sender, RoutedEventArgs e)
    {
        if (Board is not null) Board.EventTemplate = CustomTemplateToggle.IsOn ? (DataTemplate)Root.Resources["CustomEventTemplate"] : null;
    }
    private void Theme_Toggled(object sender, RoutedEventArgs e) => Root.RequestedTheme = DarkToggle.IsOn ? ElementTheme.Dark : ElementTheme.Light;
    private void Selection_Toggled(object sender, RoutedEventArgs e)
    {
        if (Board is not null) Board.SelectionMode = SelectionToggle.IsOn ? CalendarSelectionMode.Single : CalendarSelectionMode.None;
    }
    private void EventSelection_Toggled(object sender, RoutedEventArgs e)
    {
        if (Board is not null) Board.IsEventSelectionEnabled = EventSelectionToggle.IsOn;
    }
    private void EventInvocation_Toggled(object sender, RoutedEventArgs e)
    {
        if (Board is not null) Board.IsEventInvocationEnabled = EventInvocationToggle.IsOn;
    }
    private void TemplateAction_Click(object sender, RoutedEventArgs e)
    {
#if CALENDAR_PACKAGE_CHECKS
        _nestedInvocations++;
#endif
        var context = (sender as FrameworkElement)?.DataContext as CalendarEventContext;
        Status.Text = Text("TemplateStatus", context?.Title, _dateInvocations, _eventInvocations);
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
    private void Range_Changed(object? sender, CalendarRangeChangedEventArgs e)
    {
        if (MonthTitle is null || Board is null) return;
        UpdateTitle();
    }
    private void UpdateTitle()
    {
        MonthTitle.Text = Board.DisplayDate.ToString("Y", _uiCulture);
        RangeStatus.Text = Text("Range", Board.PrimaryRange.Start, Board.PrimaryRange.EndExclusive, Board.VisibleRange.Start, Board.VisibleRange.EndExclusive);
    }
    private sealed record WorkItem(string Name, DateOnly Start, DateOnly EndExclusive);
    private sealed class ExampleAdapter : ICalendarEventAdapter
    {
        public CalendarEventData GetEvent(object source) => source is WorkItem work
            ? CalendarEventData.AllDay(work.Start, work.EndExclusive) : DefaultCalendarEventAdapter.Instance.GetEvent(source);
        public string GetDisplayText(object source) => source is WorkItem work ? work.Name : DefaultCalendarEventAdapter.Instance.GetDisplayText(source);
    }
}
