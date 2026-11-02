using System.Globalization;
using Calendar.Core.Dates;
using Calendar.Core.Views.Month;
using Calendar.WinUI.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Calendar.WinUI.Controls;

public sealed partial class Calendar
{
    private bool _structureDirty = true;
    private bool _eventsDirty = true;
    private bool _datesDirty = true;
    private MonthGrid? _renderedGrid;
    private int _renderedCapacity;
    private readonly List<Grid> _weekRoots = new();
    private readonly List<Grid> _overlays = new();
    private readonly List<FrameworkElement> _separators = new();
    private readonly Dictionary<DateOnly, MoreVisual> _morePresenters = new();

    private sealed class EventVisual
    {
        public required MonthSegment Segment { get; set; }
        public required CalendarEventContext Context { get; set; }
        public required ContentControl Control { get; init; }
        public required Border Outline { get; init; }
        public required Grid Overlay { get; init; }
        public DateOnly? PointerDate { get; set; }
    }

    private sealed class MoreVisual
    {
        public required MonthDayEvents Day { get; set; }
        public required Button Control { get; init; }
        public required Grid Overlay { get; init; }
    }

    private int GetCapacity(int weekCount, double actualHeight)
    {
        var options = MonthOptions;
        var height = Math.Max(weekCount * (options.DateHeaderHeight + options.EventRowHeight), actualHeight - 32);
        return Math.Max(1, (int)Math.Floor((height / weekCount - options.DateHeaderHeight) / options.EventRowHeight));
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Star columns handle width changes. Only a new event-row capacity needs application work.
        if (e.NewSize.Height == e.PreviousSize.Height) return;
        if (_renderedGrid is not null && GetCapacity(_renderedGrid.WeekCount, e.NewSize.Height) == _renderedCapacity) return;
        QueueRender();
    }

    private void Render()
    {
        if (_root is null) return;
        if (_dataDirty && _deferRefresh == 0) ProcessData();
        var grid = CreateGrid();
        var rebuild = _structureDirty || !ReferenceEquals(grid, _renderedGrid);
        var capacity = GetCapacity(grid.WeekCount, ActualHeight);
        if (!rebuild && !_eventsDirty && !_datesDirty && capacity == _renderedCapacity) return;
        var previousFocus = (XamlRoot is null ? null : FocusManager.GetFocusedElement(XamlRoot)) as FrameworkElement;
        var focusKey = previousFocus is not null && IsDescendant(previousFocus, this) ? AutomationProperties.GetAutomationId(previousFocus) : null;

        if (rebuild)
        {
            BuildDateGrid(grid);
            _renderedGrid = grid;
            _structureDirty = false;
            _eventsDirty = true;
            _datesDirty = true;
        }
        for (var row = 1; row < _root.RowDefinitions.Count; row++)
            _root.RowDefinitions[row].MinHeight = MonthOptions.DateHeaderHeight + MonthOptions.EventRowHeight;
        if (_eventsDirty || capacity != _renderedCapacity)
        {
            var layout = MonthLayoutEngine.Build(grid, _occurrences, capacity);
            UpdateEvents(layout, capacity);
            _eventsDirty = false;
            _renderedCapacity = capacity;
        }
        if (_datesDirty)
        {
            RefreshDateStyles();
            _datesDirty = false;
        }
        if (focusKey is not null && _focusTargets.TryGetValue(focusKey, out var restore) && !ReferenceEquals(previousFocus, restore))
            restore.Focus(FocusState.Programmatic);
    }

    private void BuildDateGrid(MonthGrid grid)
    {
        _root!.Children.Clear();
        _root.RowDefinitions.Clear();
        _root.RowDefinitions.Add(new() { Height = new GridLength(32) });
        for (var i = 0; i < grid.WeekCount; i++) _root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        _days.Clear(); _dayOutlines.Clear(); _focusTargets.Clear(); _eventPresenters.Clear();
        _morePresenters.Clear(); _weekRoots.Clear(); _overlays.Clear(); _separators.Clear();
        if (!_focusDate.HasValue || !grid.Range.VisibleRange.Contains(_focusDate.Value))
            _focusDate = SelectedDate.HasValue && grid.Range.VisibleRange.Contains(ToDate(SelectedDate.Value)) ? ToDate(SelectedDate.Value) : ToDate(DisplayDate);

        var headers = SevenColumns();
        headers.BorderThickness = new Thickness(0, 0, 0, 1);
        headers.BorderBrush = _root.BorderBrush;
        _separators.Add(headers);
        for (var col = 0; col < 7; col++)
        {
            var dow = (DayOfWeek)(((int)FirstDayOfWeek + col) % 7);
            var header = new ContentControl
            {
                Content = new CalendarDayOfWeekContext { DayOfWeek = dow, Text = DisplayCulture.DateTimeFormat.GetAbbreviatedDayName(dow) },
                ContentTemplate = DayOfWeekHeaderTemplate ?? GetPartTemplate("DefaultDayOfWeekTemplate"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0), IsTabStop = false
            };
            var headerBorder = new Border
            {
                Child = header, BorderThickness = new Thickness(0, 0, col < 6 ? 1 : 0, 0), BorderBrush = _root.BorderBrush
            };
            _separators.Add(headerBorder);
            Grid.SetColumn(headerBorder, col); headers.Children.Add(headerBorder);
        }
        _root.Children.Add(headers);
        for (var row = 0; row < grid.WeekCount; row++)
        {
            var weekRoot = SevenColumns();
            Grid.SetRow(weekRoot, row + 1);
            for (var col = 0; col < 7; col++)
            {
                var date = grid.Range.VisibleRange.Start.AddDays(row * 7 + col);
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top,
                    IsTabStop = date == _focusDate, Tag = date
                };
                SetId(button, $"CalendarDate_{date:yyyy-MM-dd}");
                button.Click += (_, _) => InvokeDate(date, button);
                button.AddHandler(KeyDownEvent, new KeyEventHandler(DateKeyDown), true);
                _days.Add(date, button);
                var cell = new Grid
                {
                    BorderThickness = new Thickness(0, 0, col < 6 ? 1 : 0, row < grid.WeekCount - 1 ? 1 : 0),
                    BorderBrush = _root.BorderBrush
                };
                _separators.Add(cell);
                cell.Children.Add(button);
                var outline = new Border { Style = GetPartStyle("CalendarDateSelectionOutlineStyle"), IsHitTestVisible = false };
                cell.Children.Add(outline); _dayOutlines.Add(date, outline);
                Grid.SetColumn(cell, col); weekRoot.Children.Add(cell);
            }
            var overlay = SevenColumns();
            Grid.SetColumnSpan(overlay, 7);
            weekRoot.Children.Add(overlay); _weekRoots.Add(weekRoot); _overlays.Add(overlay);
            _root.Children.Add(weekRoot);
        }
    }

    private void RefreshSeparators()
    {
        if (_root is null) return;
        foreach (var element in _separators)
        {
            if (element is Grid grid) grid.BorderBrush = _root.BorderBrush;
            else if (element is Border border) border.BorderBrush = _root.BorderBrush;
        }
    }

    private void UpdateEvents(MonthLayout layout, int capacity)
    {
        _activeContainerStyles.Clear();
        try { UpdateEventVisuals(layout, capacity); }
        finally { PruneContainerStyles(); }
    }

    private void UpdateEventVisuals(MonthLayout layout, int capacity)
    {
        var primaryRange = layout.Grid.Range.PrimaryRange;
        var activeEvents = new HashSet<(int Week, int Source)>();
        var activeMore = new HashSet<DateOnly>();
        for (var row = 0; row < layout.Weeks.Count; row++)
        {
            var week = layout.Weeks[row];
            var overlay = _overlays[row];
            // Keep row definitions as well as controls when possible.
            while (overlay.RowDefinitions.Count > capacity + 2) overlay.RowDefinitions.RemoveAt(overlay.RowDefinitions.Count - 1);
            while (overlay.RowDefinitions.Count < capacity + 2) overlay.RowDefinitions.Add(new());
            overlay.RowDefinitions[0].Height = new GridLength(MonthOptions.DateHeaderHeight);
            for (var i = 1; i <= capacity; i++) overlay.RowDefinitions[i].Height = new GridLength(MonthOptions.EventRowHeight);
            overlay.RowDefinitions[capacity + 1].Height = new GridLength(1, GridUnitType.Star);
            foreach (var segment in week.Segments)
            {
                var key = (row, segment.Occurrence.SourceIndex);
                activeEvents.Add(key);
                if (_eventPresenters.TryGetValue(key, out var visual) && (visual.Control is Button) != HasEventInteraction)
                {
                    RemoveEvent(key, visual);
                    visual = null;
                }
                if (visual is null)
                {
                    visual = CreateEventVisual(segment, row, primaryRange);
                    _eventPresenters.Add(key, visual);
                    overlay.Children.Add(visual.Control); overlay.Children.Add(visual.Outline);
                }
                else if (!ReferenceEquals(visual.Segment.Occurrence, segment.Occurrence) || visual.Segment.Dates != segment.Dates)
                {
                    visual.Context = new CalendarEventContext { Occurrence = segment.Occurrence, DisplayedDates = segment.Dates };
                    visual.Control.Content = visual.Context;
                    visual.Control.DataContext = visual.Context;
                    AutomationProperties.SetName(visual.Control, $"{visual.Context.Title}, {segment.Occurrence.Dates.Start.ToString("D", DisplayCulture)} – {segment.Occurrence.Dates.EndExclusive.AddDays(-1).ToString("D", DisplayCulture)}");
                    visual.PointerDate = null;
                }
                visual.Segment = segment;
                ApplyEventStyle(visual);
                visual.Control.ContentTemplate = EventTemplate ?? GetPartTemplate("DefaultEventTemplate");
                Grid.SetColumn(visual.Control, segment.StartColumn); Grid.SetColumnSpan(visual.Control, segment.ColumnSpan);
                Grid.SetRow(visual.Control, segment.Lane + 1);
                Grid.SetColumn(visual.Outline, segment.StartColumn); Grid.SetColumnSpan(visual.Outline, segment.ColumnSpan);
                Grid.SetRow(visual.Outline, segment.Lane + 1);
            }
            foreach (var day in week.Days.Where(d => d.HiddenCount > 0))
            {
                activeMore.Add(day.Date);
                if (!_morePresenters.TryGetValue(day.Date, out var visual))
                {
                    var button = new Button { Style = GetPartStyle("CalendarMoreButtonStyle") };
                    visual = new MoreVisual { Day = day, Control = button, Overlay = overlay };
                    var current = visual;
                    button.Click += (_, _) => MoreInvoked?.Invoke(this, new(Transport(current.Day.Date), !primaryRange.Contains(current.Day.Date), current.Day.Events, button));
                    SetId(button, $"CalendarMore_{day.Date:yyyy-MM-dd}");
                    _morePresenters.Add(day.Date, visual); overlay.Children.Add(button);
                }
                visual.Day = day;
                visual.Control.Content = string.Format(DisplayCulture, MoreTextFormat ?? GetString("MoreFormat"), day.HiddenCount);
                AutomationProperties.SetName(visual.Control, $"{day.Date.ToString("D", DisplayCulture)}, {visual.Control.Content}");
                Grid.SetColumn(visual.Control, day.Date.DayNumber - week.Dates.Start.DayNumber);
                Grid.SetRow(visual.Control, capacity);
            }
        }
        foreach (var pair in _eventPresenters.Where(p => !activeEvents.Contains(p.Key)).ToArray()) RemoveEvent(pair.Key, pair.Value);
        foreach (var pair in _morePresenters.Where(p => !activeMore.Contains(p.Key)).ToArray())
        {
            pair.Value.Overlay.Children.Remove(pair.Value.Control);
            _focusTargets.Remove(AutomationProperties.GetAutomationId(pair.Value.Control));
            _morePresenters.Remove(pair.Key);
        }
        RefreshEventStyles();
    }

    private EventVisual CreateEventVisual(MonthSegment segment, int row, DateRange primaryRange)
    {
        var context = new CalendarEventContext { Occurrence = segment.Occurrence, DisplayedDates = segment.Dates };
        ContentControl control = HasEventInteraction ? new Button() : new ContentControl();
        control.Content = context;
        control.DataContext = context;
        control.IsTabStop = HasEventInteraction;
        var id = $"CalendarEvent_{row}_{segment.Occurrence.SourceIndex}";
        if (control is Button button) SetId(button, id); else AutomationProperties.SetAutomationId(control, id);
        AutomationProperties.SetName(control, $"{context.Title}, {segment.Occurrence.Dates.Start.ToString("D", DisplayCulture)} – {segment.Occurrence.Dates.EndExclusive.AddDays(-1).ToString("D", DisplayCulture)}");
        var visual = new EventVisual
        {
            Segment = segment, Context = context, Control = control, Overlay = _overlays[row],
            Outline = new Border { Style = GetPartStyle("CalendarEventSelectionOutlineStyle"), IsHitTestVisible = false }
        };
        var weekRoot = _weekRoots[row];
        var weekStart = _renderedGrid!.Range.VisibleRange.Start.AddDays(row * 7);
        control.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            var x = e.GetCurrentPoint(weekRoot).Position.X;
            if (FlowDirection == FlowDirection.RightToLeft) x = weekRoot.ActualWidth - x;
            var column = 0;
            while (column < 6 && x >= weekRoot.ColumnDefinitions[column].ActualWidth)
            {
                x -= weekRoot.ColumnDefinitions[column].ActualWidth; column++;
            }
            var current = visual.Segment;
            visual.PointerDate = weekStart.AddDays(Math.Clamp(column, current.StartColumn, current.StartColumn + current.ColumnSpan - 1));
        }), true);
        control.AddHandler(KeyDownEvent, new KeyEventHandler((_, _) => visual.PointerDate = null), true);
        if (control is Button eventButton) eventButton.Click += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject origin && IsNestedInput(origin, eventButton)) return;
            var current = visual.Segment;
            var date = visual.PointerDate ?? (_focusDate.HasValue && current.Dates.Contains(_focusDate.Value) ? _focusDate.Value : current.Dates.Start);
            visual.PointerDate = null;
            if (IsEventSelectionEnabled) SelectedEvent = current.Occurrence;
            if (IsEventInvocationEnabled) EventInvoked?.Invoke(this, new(Transport(date), !primaryRange.Contains(date), current.Occurrence, control));
        };
        return visual;
    }

    private void RemoveEvent((int Week, int Source) key, EventVisual visual)
    {
        visual.Overlay.Children.Remove(visual.Control); visual.Overlay.Children.Remove(visual.Outline);
        _focusTargets.Remove(AutomationProperties.GetAutomationId(visual.Control));
        _eventPresenters.Remove(key);
    }
}
