using System.Collections.ObjectModel;
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
    private DateOnly InitialDisplayDate =>
#if CALENDAR_PACKAGE_CHECKS
        TestDate;
#else
        Board.Today;
#endif

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1200, 1000));
        Board.EventAdapter = new ExampleAdapter();
        Board.DisplayDateOnly = InitialDisplayDate;
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
        var referenceDate = InitialDisplayDate;
        var month = new DateOnly(referenceDate.Year, referenceDate.Month, 1);
#if CALENDAR_PACKAGE_CHECKS
        AddRegressionEvents(month);
#else
        for (var offset = -1; offset <= 1; offset++) AddMonthEvents(month.AddMonths(offset));
#endif
    }
    private void Previous_Click(object sender, RoutedEventArgs e) => Board.DisplayDate = Board.DisplayDate.AddMonths(-1);
    private void Next_Click(object sender, RoutedEventArgs e) => Board.DisplayDate = Board.DisplayDate.AddMonths(1);
    private void Today_Click(object sender, RoutedEventArgs e) => Board.DisplayDateOnly = Board.Today;
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Board.DisplayDateOnly = InitialDisplayDate;
        Seed();
        ResetDiagnostics();
    }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var date = Board.SelectedDateOnly ?? Board.DisplayDateOnly;
        _events.Add(new CalendarEvent($"New event {_events.Count + 1}", CalendarEventData.AllDay(date)));
        EventAdded(date);
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
    private void TemplateAction_Click(object sender, RoutedEventArgs e) => TemplateActionInvoked(sender);
    partial void TemplateActionInvoked(object sender);
    partial void EventAdded(DateOnly date);
    partial void ResetDiagnostics();
    partial void RefreshDiagnostics();
    private void Range_Changed(object? sender, CalendarRangeChangedEventArgs e)
    {
        if (MonthTitle is null || Board is null) return;
        UpdateTitle();
    }
    private void UpdateTitle()
    {
        MonthTitle.Text = Board.DisplayDate.ToString("Y", _uiCulture);
    }
    private sealed record WorkItem(string Name, DateOnly Start, DateOnly EndExclusive);
    private sealed class ExampleAdapter : ICalendarEventAdapter
    {
        public CalendarEventData GetEvent(object source) => source is WorkItem work
            ? CalendarEventData.AllDay(work.Start, work.EndExclusive) : DefaultCalendarEventAdapter.Instance.GetEvent(source);
        public string GetDisplayText(object source) => source is WorkItem work ? work.Name : DefaultCalendarEventAdapter.Instance.GetDisplayText(source);
    }
}
