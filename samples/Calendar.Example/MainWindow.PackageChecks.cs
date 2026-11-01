#if CALENDAR_PACKAGE_CHECKS
using System.Collections.ObjectModel;
using Calendar.Core.Events;
using Calendar.WinUI.Controls;
using Calendar.WinUI.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;

namespace Calendar.Example;

public sealed partial class MainWindow
{
    private int _rangeChanges;
    private bool _lastOldRangeWasNull;
    private bool _checksStarted;
    private int _nestedInvocations;
    private static readonly DateOnly TestDate = new(2026, 10, 28);
    private readonly TextBlock ApiStatus = new() { Text = "API checks: running", TextWrapping = TextWrapping.Wrap };

    private void InitializePackageChecks()
    {
        Require(Board.DisplayLanguage is null && Board.DisplayTimeZone.Id == TimeZoneInfo.Local.Id, "Calendar defaults must follow the system.");
        Require(Board.IsEventSelectionEnabled && Board.IsEventInvocationEnabled, "Event input defaults must be enabled.");
        AutomationProperties.SetAutomationId(ApiStatus, "ApiRegressionStatus");
        var runtime = new TextBlock { Text = $"NuGet example · .NET {Environment.Version} · no ProjectReference" };
        AutomationProperties.SetAutomationId(runtime, "RuntimeVersion");
        StatusPanel.Children.Insert(0, runtime);
        StatusPanel.Children.Insert(0, ApiStatus);
        Board.VisibleRangeChanged += (_, e) => { _rangeChanges++; _lastOldRangeWasNull = e.OldRange is null; };
        Board.Loaded += RunApiChecks;
    }

    private static Style PackageCheckStyle() => (Style)XamlReader.Load("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ContentControl">
            <Setter Property="Tag" Value="Custom event style" />
            <Setter Property="Padding" Value="6,0" />
        </Style>
        """);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private Task NextTurn()
    {
        var completion = new TaskCompletionSource<bool>();
        if (!DispatcherQueue.TryEnqueue(() => completion.SetResult(true))) throw new InvalidOperationException("Dispatcher unavailable.");
        return completion.Task;
    }

    private async Task DrainUpdates() { await NextTurn(); Board.UpdateLayout(); await NextTurn(); Board.UpdateLayout(); }

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element) yield return element;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private ContentControl EventContainer() => Descendants(Board).OfType<ContentControl>()
        .FirstOrDefault(element => AutomationProperties.GetAutomationId(element) == "CalendarEvent_4_0")
        ?? throw new InvalidOperationException("The expected event container was not rendered.");

    private static void Activate(Button button)
    {
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(button) ?? new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
    }

    private async void RunApiChecks(object sender, RoutedEventArgs e)
    {
        if (_checksStarted) return;
        _checksStarted = true;
        var results = new List<(string Name, string? Error)>();
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); results.Add((name, null)); }
            catch (Exception error) { results.Add((name, error.Message)); }
        }

        await Check("initial range and automatic weeks", async () =>
        {
            Require(_rangeChanges == 1 && _lastOldRangeWasNull, "Initial range must be delivered exactly once on load.");
            Board.MonthOptions = new MonthViewOptions();
            Require(_rangeChanges == 1, "Sizing options changed the range.");
            Board.DisplayDateOnly = TestDate.AddMonths(-2);
            Require(_rangeChanges == 2 && Board.VisibleRange.DayCount == 42, "August must have six weeks.");
            Board.DisplayDateOnly = TestDate;
            Require(_rangeChanges == 3 && Board.VisibleRange.DayCount == 35, "October must have five weeks.");
            await DrainUpdates();
        });

        await Check("source before adapter and pending selection", async () =>
        {
            var item = new Appointment("id", "Meeting", TestDate);
            Board.EventsSource = new[] { item };
            Board.EventAdapter = CalendarEventAdapter.Create<Appointment>(
                data: model => CalendarEventData.AllDay(model.Date), text: model => model.Title, key: model => model.Id);
            Board.SelectedItem = item;
            await DrainUpdates();
            Require(ReferenceEquals(Board.SelectedOccurrence?.Source, item), "Pending source selection was not resolved.");
        });

        await Check("stable keys survive replacement and clear on removal", async () =>
        {
            var replacements = 0;
            var selectionChanges = 0;
            EventHandler<global::Calendar.WinUI.Interaction.CalendarItemSelectionChangedEventArgs> itemChanged = (_, _) => replacements++;
            EventHandler<global::Calendar.WinUI.Interaction.CalendarEventSelectionChangedEventArgs> selectionChanged = (_, _) => selectionChanges++;
            Board.SelectedItemChanged += itemChanged;
            Board.SelectedEventChanged += selectionChanged;
            try
            {
                var replacement = new Appointment("id", "Rescheduled", TestDate.AddDays(1));
                Board.EventsSource = new[] { replacement };
                await DrainUpdates();
                Require(ReferenceEquals(Board.SelectedItem, replacement) && Board.SelectedOccurrence?.DisplayText == "Rescheduled", "Stable-key replacement lost selection.");
                Require(replacements == 1 && selectionChanges == 0, "Same-key replacement must notify source replacement without a new logical selection.");
                Board.EventsSource = Array.Empty<Appointment>();
                await DrainUpdates();
                Require(Board.SelectedItem is null && Board.SelectedOccurrence is null && selectionChanges == 1, "Removed selection was not cleared once.");
            }
            finally { Board.SelectedItemChanged -= itemChanged; Board.SelectedEventChanged -= selectionChanged; }
        });

        await Check("batch projection and incremental notifications", async () =>
        {
            var calls = 0;
            var items = new ObservableCollection<CalendarEvent>();
            var pending = new CalendarEvent("Event 0", CalendarEventData.AllDay(TestDate));
            using (Board.DeferRefresh())
            {
                Board.EventsSource = items;
                Board.SelectedItem = pending;
                Board.EventAdapter = CalendarEventAdapter.Create<CalendarEvent>(
                    data: model => { calls++; return model.Data; }, text: model => model.Title);
                items.Add(pending);
                for (var i = 1; i < 1000; i++) items.Add(new($"Event {i}", CalendarEventData.AllDay(TestDate)));
                Require(calls == 0, "Data was projected inside a refresh scope.");
            }
            Require(calls == 1000, "Batch did not project exactly once per item.");
            Require(ReferenceEquals(Board.SelectedOccurrence?.Source, pending), "Selection was evaluated using the previous adapter during initialization.");
            await DrainUpdates();
            Require(calls == 1000, "Queued updates repeated a completed batch.");
            calls = 0;
            items[10].Title = "Updated";
            await DrainUpdates();
            Require(calls == 1, "Changing one model re-adapted unrelated models.");
            calls = 0;
            items.Add(new("Added", CalendarEventData.AllDay(TestDate)));
            await DrainUpdates();
            Require(calls == 1, "Adding one model re-adapted existing models.");
            calls = 0;
            items.Move(0, 20);
            await DrainUpdates();
            Require(calls == 0, "Reordering unchanged models called the adapter.");
            var outer = Board.DeferRefresh();
            using (Board.DeferRefresh()) items[0].Title = "Nested update";
            Require(calls == 0, "Nested scope refreshed too early.");
            outer.Dispose(); outer.Dispose();
            Require(calls == 1, "Outermost scope was not idempotent or processed more than once.");
        });

        await Check("editing selected source and legacy selection API", async () =>
        {
            var item = ((ObservableCollection<CalendarEvent>)Board.EventsSource!)[0];
            Board.SelectedItem = item;
            var changes = 0;
            EventHandler<global::Calendar.WinUI.Interaction.CalendarEventSelectionChangedEventArgs> changed = (_, _) => changes++;
            Board.SelectedEventChanged += changed;
            try
            {
                item.Data = CalendarEventData.AllDay(TestDate.AddDays(1));
                await DrainUpdates();
                Require(ReferenceEquals(Board.SelectedItem, item) && Board.SelectedOccurrence?.Data == item.Data && changes == 0, "Editing data changed logical selection.");
                Board.RefreshEvents();
                Require(changes == 0, "Explicit refresh raised a duplicate selection notification.");
                Board.SelectedEvent = null;
                Require(Board.SelectedItem is null && changes == 1, "Legacy selection clearing did not synchronize.");
                Board.SelectedEvent = CalendarOccurrence.Project(item, item.Data, item.Title, 0, Board.DisplayTimeZone);
                Require(ReferenceEquals(Board.SelectedItem, item) && changes == 2, "Legacy projected selection did not synchronize.");
                Board.SelectedItem = null;
            }
            finally { Board.SelectedEventChanged -= changed; }
        });

        await Check("date helpers and application minimum size", async () =>
        {
            Board.DisplayTimeZone = TimeZoneInfo.Utc;
            Require(Board.Today == DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime), "Today did not use the display zone.");
            Board.DisplayTimeZone = TimeZoneInfo.CreateCustomTimeZone("UTC+14", TimeSpan.FromHours(14), "UTC+14", "UTC+14");
            Require(Board.Today == DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(14)).DateTime), "Today ignored the changed zone.");
            Board.ClearValue(global::Calendar.WinUI.Controls.Calendar.DisplayTimeZoneProperty);
            Board.SelectedDateOnly = TestDate;
            Require(Board.SelectedDate?.Offset == TimeSpan.Zero && Board.SelectedDateOnly == TestDate, "Selected date helper converted an instant.");
            Board.SelectedDateOnly = null;
            Require(Board.SelectedDate is null, "Date-only selection could not be cleared.");
            Board.MinWidth = 480; Board.MinHeight = 400;
            Board.DisplayDateOnly = TestDate.AddMonths(-2);
            await DrainUpdates();
            Board.DisplayDateOnly = TestDate;
            await DrainUpdates();
            Require(Board.MinWidth == 480 && Board.MinHeight == 400, "Rendering overwrote the application's minimum size.");
            Board.ClearValue(FrameworkElement.MinWidthProperty); Board.ClearValue(FrameworkElement.MinHeightProperty);
        });

        var eventItem = new CalendarEvent("Visible event", CalendarEventData.AllDay(TestDate));
        Board.SelectedItem = null;
        Board.EventsSource = new[] { eventItem };
        Board.EventAdapter = DefaultCalendarEventAdapter.Instance;
        await DrainUpdates();

        await Check("container styles retain built-in templates", async () =>
        {
            Board.EventContainerStyle = (Style)PackageCheckStyle();
            await DrainUpdates();
            Require(EventContainer().Tag is string tag && tag == "Custom event style" && EventContainer().Padding.Left == 6,
                "XAML style setters did not apply to the event container.");
            var additional = new Style(typeof(ContentControl));
            additional.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 0, 9, 0)));
            Board.EventContainerStyle = additional;
            await DrainUpdates();
            Require(EventContainer().Padding.Left == 9 && EventContainer().ContentTemplate is not null, "Additional style replaced built-in content or was ignored.");
            Board.EventContainerStyleSelector = new RegressionStyleSelector();
            await DrainUpdates();
            Require(EventContainer().Padding.Left == 13 && EventContainer().DataContext is CalendarEventContext, "Selector did not receive the event context.");
            Board.IsEventSelectionEnabled = Board.IsEventInvocationEnabled = false;
            await DrainUpdates();
            Require(EventContainer() is not Button && EventContainer().Padding.Left == 13 && EventContainer().IsEnabled, "Display-only style or enabled content was lost.");
            Board.IsEventSelectionEnabled = Board.IsEventInvocationEnabled = true;
            Board.EventContainerStyle = null; Board.EventContainerStyleSelector = null;
            await DrainUpdates();
            Require(EventContainer().Padding.Left == 4, "Clearing custom styles did not restore the default.");
        });

        await Check("independent selection invocation and nested actions", async () =>
        {
            var calls = 0;
            EventHandler<global::Calendar.WinUI.Interaction.CalendarEventInvokedEventArgs> invoked = (_, _) => calls++;
            Board.EventInvoked += invoked;
            try
            {
                Board.SelectedItem = null;
                Board.IsEventSelectionEnabled = false;
                Board.IsEventInvocationEnabled = true;
                await DrainUpdates();
                Activate((Button)EventContainer());
                await DrainUpdates();
                Require(calls == 1 && Board.SelectedItem is null, "Invocation-only activation selected the event.");
                Board.IsEventSelectionEnabled = true;
                Board.IsEventInvocationEnabled = false;
                Activate((Button)EventContainer());
                await DrainUpdates();
                Require(calls == 1 && ReferenceEquals(Board.SelectedItem, eventItem), "Selection-only activation invoked the event.");
                Board.SelectedItem = null;
                Board.IsEventSelectionEnabled = false;
                Board.EventTemplate = (DataTemplate)Root.Resources["CustomEventTemplate"];
                await DrainUpdates();
                var nested = Descendants(Board).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "TemplateAction_0_2026-10-28");
                Require(nested.IsEnabled, "Read-only event disabled an independent nested action.");
                Activate(nested);
                await DrainUpdates();
                Require(_nestedInvocations == 1 && calls == 1 && Board.SelectedItem is null, "Nested action invoked or selected its display-only parent.");
                Board.IsEventSelectionEnabled = Board.IsEventInvocationEnabled = true;
                await DrainUpdates();
                nested = Descendants(Board).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "TemplateAction_0_2026-10-28");
                Activate(nested);
                await DrainUpdates();
                Require(_nestedInvocations == 2 && calls == 1 && Board.SelectedItem is null, "Nested action invoked or selected its interactive parent.");
            }
            finally { Board.EventInvoked -= invoked; }
        });

        Board.IsEventSelectionEnabled = Board.IsEventInvocationEnabled = true;
        Board.EventTemplate = null; Board.EventContainerStyle = null; Board.EventContainerStyleSelector = null;
        Board.SelectedItem = null;
        Board.SelectedDateOnly = null;
        Board.EventsSource = _events;
        Board.EventAdapter = new ExampleAdapter();
        Board.DisplayDateOnly = TestDate;
        Seed();
        _dateInvocations = _eventInvocations = _eventSelectionChanges = 0;
        LanguagePicker.SelectedIndex = 2;
        Status.Text = Text("InitialStatus");
        UpdateEventSelectionStatus();
        UpdateTitle();
        await DrainUpdates();
        var failures = results.Where(result => result.Error is not null).ToArray();
        ApiStatus.Text = failures.Length == 0 ? $"API checks: PASS ({results.Count})"
            : "API checks: FAIL " + string.Join("; ", failures.Select(result => $"{result.Name}: {result.Error}"));
    }

    private sealed record Appointment(string Id, string Title, DateOnly Date);
    private sealed class RegressionStyleSelector : StyleSelector
    {
        private readonly Style _style = new(typeof(ContentControl)) { Setters = { new Setter(Control.PaddingProperty, new Thickness(13, 0, 13, 0)) } };
        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            Require(item is CalendarEventContext, "Style selector item was not an event context.");
            return _style;
        }
    }
}

#endif
