using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Calendar.Core.Dates;
using Calendar.Core.Events;
using Calendar.Core.Views;
using Calendar.Core.Views.Month;
using Calendar.WinUI.Interaction;
using Calendar.WinUI.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Calendar.WinUI.Controls;

/// <summary>Month calendar with application-owned data and interaction. No navigation chrome or dialogs.</summary>
public sealed partial class Calendar : Control
{
    private Grid? _root;
    private long _borderBrushToken;
    private bool _pending;
    private bool _loaded;
    private readonly HashSet<INotifyPropertyChanged> _observed = new(ReferenceEqualityComparer.Instance);
    private INotifyCollectionChanged? _collection;
    private MonthViewOptions? _observedOptions;
    private IReadOnlyList<CalendarOccurrence> _occurrences = Array.Empty<CalendarOccurrence>();
    private readonly Dictionary<DateOnly, Button> _days = new();
    private readonly Dictionary<DateOnly, Border> _dayOutlines = new();
    private readonly Dictionary<string, Button> _focusTargets = new();
    private readonly Dictionary<(int Week, int Source), EventVisual> _eventPresenters = new();
    private DateOnly? _focusDate;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _todayTimer;
    private DateOnly _today;

    /// <summary>Initializes a month calendar with system language and time-zone defaults.</summary>
    public Calendar()
    {
        DefaultStyleKey = typeof(Calendar);
        SetValue(DisplayDateProperty, Transport(DateOnly.FromDateTime(DateTime.Today)));
        SetValue(FirstDayOfWeekProperty, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
        MonthOptions = new MonthViewOptions();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        IsTabStop = false;
    }

    /// <summary>Identifies the <see cref="DisplayUnit"/> dependency property.</summary>
    public static readonly DependencyProperty DisplayUnitProperty = Register(nameof(DisplayUnit), typeof(CalendarDisplayUnit), CalendarDisplayUnit.Month);
    /// <summary>Identifies the <see cref="DisplayDate"/> dependency property.</summary>
    public static readonly DependencyProperty DisplayDateProperty = Register(nameof(DisplayDate), typeof(DateTimeOffset), default(DateTimeOffset));
    /// <summary>Identifies the <see cref="SelectedDate"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(nameof(SelectedDate), typeof(DateTimeOffset?), typeof(Calendar), new PropertyMetadata(null, SelectionChanged));
    /// <summary>Identifies the <see cref="SelectionMode"/> dependency property.</summary>
    public static readonly DependencyProperty SelectionModeProperty = Register(nameof(SelectionMode), typeof(CalendarSelectionMode), CalendarSelectionMode.Single);
    /// <summary>Identifies the <see cref="IsEventSelectionEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsEventSelectionEnabledProperty = Register(nameof(IsEventSelectionEnabled), typeof(bool), true);
    /// <summary>Identifies the <see cref="IsEventInvocationEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsEventInvocationEnabledProperty = Register(nameof(IsEventInvocationEnabled), typeof(bool), true);
    /// <summary>Identifies the <see cref="SelectedItem"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(Calendar), new PropertyMetadata(null, ItemSelectionChanged));
    /// <summary>Identifies the <see cref="SelectedEvent"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedEventProperty = DependencyProperty.Register(
        nameof(SelectedEvent), typeof(object), typeof(Calendar), new PropertyMetadata(null, EventSelectionChanged));
    /// <summary>Identifies the <see cref="FirstDayOfWeek"/> dependency property.</summary>
    public static readonly DependencyProperty FirstDayOfWeekProperty = Register(nameof(FirstDayOfWeek), typeof(DayOfWeek), DayOfWeek.Sunday);
    /// <summary>Identifies the <see cref="DisplayTimeZone"/> dependency property.</summary>
    public static readonly DependencyProperty DisplayTimeZoneProperty = DependencyProperty.Register(nameof(DisplayTimeZone), typeof(object), typeof(Calendar), new PropertyMetadata(null, DataChanged));
    /// <summary>Identifies the <see cref="DisplayLanguage"/> dependency property.</summary>
    public static readonly DependencyProperty DisplayLanguageProperty = Register(nameof(DisplayLanguage), typeof(string), null);
    /// <summary>Identifies the <see cref="EventsSource"/> dependency property.</summary>
    public static readonly DependencyProperty EventsSourceProperty = DependencyProperty.Register(nameof(EventsSource), typeof(object), typeof(Calendar), new PropertyMetadata(null, SourceChanged));
    /// <summary>Identifies the <see cref="EventAdapter"/> dependency property.</summary>
    public static readonly DependencyProperty EventAdapterProperty = DependencyProperty.Register(nameof(EventAdapter), typeof(object), typeof(Calendar), new PropertyMetadata(null, DataChanged));
    /// <summary>Identifies the <see cref="EventTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty EventTemplateProperty = Register(nameof(EventTemplate), typeof(DataTemplate), null);
    /// <summary>Identifies the <see cref="EventContainerStyle"/> dependency property.</summary>
    public static readonly DependencyProperty EventContainerStyleProperty = Register(nameof(EventContainerStyle), typeof(Style), null);
    /// <summary>Identifies the <see cref="EventContainerStyleSelector"/> dependency property.</summary>
    public static readonly DependencyProperty EventContainerStyleSelectorProperty = Register(nameof(EventContainerStyleSelector), typeof(StyleSelector), null);
    /// <summary>Identifies the <see cref="DateHeaderTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty DateHeaderTemplateProperty = Register(nameof(DateHeaderTemplate), typeof(DataTemplate), null);
    /// <summary>Identifies the <see cref="DayOfWeekHeaderTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty DayOfWeekHeaderTemplateProperty = Register(nameof(DayOfWeekHeaderTemplate), typeof(DataTemplate), null);
    /// <summary>Identifies the <see cref="MonthOptions"/> dependency property.</summary>
    public static readonly DependencyProperty MonthOptionsProperty = DependencyProperty.Register(nameof(MonthOptions), typeof(MonthViewOptions), typeof(Calendar), new PropertyMetadata(null, OptionsChanged));
    /// <summary>Identifies the <see cref="MoreTextFormat"/> dependency property.</summary>
    public static readonly DependencyProperty MoreTextFormatProperty = Register(nameof(MoreTextFormat), typeof(string), null);
    /// <summary>Identifies the <see cref="TodayText"/> dependency property.</summary>
    public static readonly DependencyProperty TodayTextProperty = Register(nameof(TodayText), typeof(string), null);

    /// <summary>Gets or sets the implemented display unit. Currently only Month is supported.</summary>
    public CalendarDisplayUnit DisplayUnit { get => (CalendarDisplayUnit)GetValue(DisplayUnitProperty); set => SetValue(DisplayUnitProperty, value); }
    /// <summary>Gets or sets the date whose month is displayed. Only calendar date components are used.</summary>
    public DateTimeOffset DisplayDate { get => (DateTimeOffset)GetValue(DisplayDateProperty); set => SetValue(DisplayDateProperty, value); }
    /// <summary>Gets or sets the selected date, or null. This is a date-only transport value, not an instant.</summary>
    public DateTimeOffset? SelectedDate { get => (DateTimeOffset?)GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    /// <summary>Gets or sets date selection behavior. The default is Single; None keeps date invocation enabled.</summary>
    public CalendarSelectionMode SelectionMode { get => (CalendarSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    /// <summary>Gets or sets whether user interaction retains event selection. Invocation is independent. The default is true.</summary>
    public bool IsEventSelectionEnabled { get => (bool)GetValue(IsEventSelectionEnabledProperty); set => SetValue(IsEventSelectionEnabledProperty, value); }
    /// <summary>Gets or sets whether event activation raises EventInvoked. Selection is independent. The default is true.</summary>
    public bool IsEventInvocationEnabled { get => (bool)GetValue(IsEventInvocationEnabledProperty); set => SetValue(IsEventInvocationEnabledProperty, value); }
    /// <summary>Gets or sets the selected original application object. An item assigned before data is available is resolved on refresh.</summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    /// <summary>The selected occurrence, independent of focus and date selection. Set null to clear.</summary>
    public CalendarOccurrence? SelectedEvent { get => GetValue(SelectedEventProperty) as CalendarOccurrence; set => SetValue(SelectedEventProperty, value); }
    /// <summary>Gets the current projected selection. This C# convenience property reflects the bindable SelectedEvent property.</summary>
    public CalendarOccurrence? SelectedOccurrence => SelectedEvent;
    /// <summary>Gets or sets DisplayDate as a DateOnly value. This C# helper does not convert UTC instants.</summary>
    public DateOnly DisplayDateOnly { get => ToDate(DisplayDate); set => DisplayDate = Transport(value); }
    /// <summary>Gets or sets SelectedDate as a nullable DateOnly value. This is a C# convenience property.</summary>
    public DateOnly? SelectedDateOnly { get => SelectedDate is { } date ? ToDate(date) : null; set => SelectedDate = value is { } date ? Transport(date) : null; }
    /// <summary>Gets today's calendar date in DisplayTimeZone at the time of access.</summary>
    public DateOnly Today => CurrentDate();
    /// <summary>Gets or sets the first weekday. The default follows the system culture.</summary>
    public DayOfWeek FirstDayOfWeek { get => (DayOfWeek)GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }
    /// <summary>Gets or sets the time zone used to project absolute events and determine today. An unset value uses TimeZoneInfo.Local.</summary>
    public TimeZoneInfo DisplayTimeZone { get => GetValue(DisplayTimeZoneProperty) as TimeZoneInfo ?? TimeZoneInfo.Local; set => SetValue(DisplayTimeZoneProperty, value); }
    /// <summary>BCP-47 language tag for calendar text and formatting. Null or empty uses the system UI culture.</summary>
    public string? DisplayLanguage { get => (string?)GetValue(DisplayLanguageProperty); set => SetValue(DisplayLanguageProperty, value); }
    private CultureInfo DisplayCulture => string.IsNullOrEmpty(DisplayLanguage) ? CultureInfo.CurrentUICulture : CultureInfo.GetCultureInfo(DisplayLanguage);
    private string GetString(string name) => CalendarStrings.Get(name, DisplayCulture.Name);
    /// <summary>Gets or sets the application-owned occurrence collection. Mutations must occur on the UI thread.</summary>
    public IEnumerable? EventsSource { get => GetValue(EventsSourceProperty) as IEnumerable; set => SetValue(EventsSourceProperty, value); }
    /// <summary>Gets or sets the adapter for application models. An unset value uses DefaultCalendarEventAdapter.Instance.</summary>
    public ICalendarEventAdapter EventAdapter { get => GetValue(EventAdapterProperty) as ICalendarEventAdapter ?? DefaultCalendarEventAdapter.Instance; set => SetValue(EventAdapterProperty, value); }
    /// <summary>Gets or sets the template for CalendarEventContext content, or null for the default.</summary>
    public DataTemplate? EventTemplate { get => GetValue(EventTemplateProperty) as DataTemplate; set => SetValue(EventTemplateProperty, value); }
    /// <summary>Gets or sets additional event-container styling. Target ContentControl to support interactive and display-only containers.</summary>
    /// <remarks>Setters are layered over the built-in style. The container DataContext is CalendarEventContext.</remarks>
    public Style? EventContainerStyle { get => GetValue(EventContainerStyleProperty) as Style; set => SetValue(EventContainerStyleProperty, value); }
    /// <summary>Gets or sets a per-segment style selector. Its item is CalendarEventContext; null results fall back to EventContainerStyle.</summary>
    public StyleSelector? EventContainerStyleSelector { get => GetValue(EventContainerStyleSelectorProperty) as StyleSelector; set => SetValue(EventContainerStyleSelectorProperty, value); }
    /// <summary>Gets or sets the template for CalendarDateContext content, or null for the default.</summary>
    public DataTemplate? DateHeaderTemplate { get => GetValue(DateHeaderTemplateProperty) as DataTemplate; set => SetValue(DateHeaderTemplateProperty, value); }
    /// <summary>Gets or sets the template for CalendarDayOfWeekContext content, or null for the default.</summary>
    public DataTemplate? DayOfWeekHeaderTemplate { get => GetValue(DayOfWeekHeaderTemplateProperty) as DataTemplate; set => SetValue(DayOfWeekHeaderTemplateProperty, value); }
    /// <summary>Gets or sets the month-only row sizing options.</summary>
    public MonthViewOptions MonthOptions { get => (MonthViewOptions)GetValue(MonthOptionsProperty); set => SetValue(MonthOptionsProperty, value ?? throw new ArgumentNullException(nameof(value))); }
    /// <summary>Gets or sets the hidden-count format with a {0} placeholder, or null for the localized default.</summary>
    public string? MoreTextFormat { get => GetValue(MoreTextFormatProperty) as string; set => SetValue(MoreTextFormatProperty, value); }
    /// <summary>Gets or sets the today label, or null for the localized default.</summary>
    public string? TodayText { get => GetValue(TodayTextProperty) as string; set => SetValue(TodayTextProperty, value); }
    /// <summary>Gets the primary month as an interval with an exclusive end.</summary>
    public DateRange PrimaryRange => CreateGrid().Range.PrimaryRange;
    /// <summary>Gets the complete visible interval, including adjacent-month dates, with an exclusive end.</summary>
    public DateRange VisibleRange => CreateGrid().Range.VisibleRange;
    private CalendarViewRange? _lastRange;

    /// <summary>Occurs whenever a date is invoked, including repeated invocation of the same date.</summary>
    public event EventHandler<CalendarDateEventArgs>? DateInvoked;
    /// <summary>Occurs when an enabled event is invoked. The original object and occurrence are preserved.</summary>
    public event EventHandler<CalendarEventInvokedEventArgs>? EventInvoked;
    /// <summary>Occurs when a more button is invoked; its payload includes all occurrences on that date.</summary>
    public event EventHandler<CalendarMoreInvokedEventArgs>? MoreInvoked;
    /// <summary>Occurs when the selected calendar date changes.</summary>
    public event EventHandler<CalendarSelectionChangedEventArgs>? SelectedDateChanged;
    /// <summary>Occurs when the selected occurrence changes, independently of date selection.</summary>
    public event EventHandler<CalendarEventSelectionChangedEventArgs>? SelectedEventChanged;
    /// <summary>Occurs when the original selected application object changes, including replacement by an object with the same key.</summary>
    public event EventHandler<CalendarItemSelectionChangedEventArgs>? SelectedItemChanged;
    /// <summary>Occurs once on initial load, then when the loaded primary or visible date range changes.</summary>
    public event EventHandler<CalendarRangeChangedEventArgs>? VisibleRangeChanged;

    private static DependencyProperty Register(string name, Type type, object? value) =>
        DependencyProperty.Register(name, type, typeof(Calendar), new PropertyMetadata(value, ViewChanged));

    private static void ViewChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var c = (Calendar)sender;
        if (c.DisplayUnit != CalendarDisplayUnit.Month) throw new NotSupportedException("Only Month is implemented.");
        if (!Enum.IsDefined(c.SelectionMode)) throw new ArgumentOutOfRangeException(nameof(SelectionMode));
        if (e.Property == DisplayLanguageProperty)
        {
            _ = c.DisplayCulture;
            c._structureDirty = c._datesDirty = c._eventsDirty = true;
        }
        c.NotifyRange();
        if (e.Property == DayOfWeekHeaderTemplateProperty) c._structureDirty = true;
        if (e.Property == DateHeaderTemplateProperty || e.Property == TodayTextProperty || e.Property == SelectionModeProperty) c._datesDirty = true;
        if (e.Property == EventContainerStyleProperty || e.Property == EventContainerStyleSelectorProperty) c._containerStyles.Clear();
        if (e.Property == EventTemplateProperty || e.Property == IsEventSelectionEnabledProperty || e.Property == IsEventInvocationEnabledProperty || e.Property == EventContainerStyleProperty || e.Property == EventContainerStyleSelectorProperty || e.Property == MoreTextFormatProperty) c._eventsDirty = true;
        c.QueueRender();
    }
    private static void DataChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var c = (Calendar)sender;
        if (e.Property == DisplayTimeZoneProperty) c._datesDirty = true;
        c.InvalidateData(allItems: true);
    }
    private static void SourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var c = (Calendar)sender;
        c.Unsubscribe();
        c._projectionCache.Clear();
        c.InvalidateData(allItems: true);
    }
    private static void SelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var c = (Calendar)sender;
        var oldDate = (DateTimeOffset?)e.OldValue;
        var newDate = (DateTimeOffset?)e.NewValue;
        if (oldDate.HasValue && newDate.HasValue && ToDate(oldDate.Value) == ToDate(newDate.Value)) return;
        if (newDate.HasValue && c._days.ContainsKey(ToDate(newDate.Value))) c.SetKeyboardDate(ToDate(newDate.Value));
        c.RefreshDateSelection(oldDate);
        c.RefreshDateSelection(newDate);
        c.SelectedDateChanged?.Invoke(c, new(oldDate, newDate));
    }
    private static void EventSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not null and not CalendarOccurrence) throw new ArgumentException("SelectedEvent must be a CalendarOccurrence or null.");
        var c = (Calendar)sender;
        if (c._synchronizingSelection) return;
        var next = e.NewValue as CalendarOccurrence;
        c.SynchronizeSelection(e.OldValue as CalendarOccurrence, next, c.SelectedItem, next?.Source);
    }
    private static void OptionsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var c = (Calendar)sender;
        c.ObserveOptions(c._loaded ? e.NewValue as MonthViewOptions : null);
        c.NotifyRange();
        c._eventsDirty = true;
        c.QueueRender();
    }
    private void OnOptionsChanged(object? sender, EventArgs e) { _eventsDirty = true; QueueRender(); }
    private void ObserveOptions(MonthViewOptions? options)
    {
        if (ReferenceEquals(_observedOptions, options)) return;
        if (_observedOptions is not null) _observedOptions.OptionsChanged -= OnOptionsChanged;
        _observedOptions = options;
        if (_observedOptions is not null) _observedOptions.OptionsChanged += OnOptionsChanged;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_root is not null) _root.UnregisterPropertyChangedCallback(Grid.BorderBrushProperty, _borderBrushToken);
        _root = GetTemplateChild("PART_Root") as Grid;
        if (_root is not null) _borderBrushToken = _root.RegisterPropertyChangedCallback(Grid.BorderBrushProperty, (_, _) => RefreshSeparators());
        _structureDirty = true;
        _containerStyles.Clear();
        Render();
    }
    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        ObserveOptions(MonthOptions);
        InvalidateData(allItems: true);
        _today = CurrentDate();
        _todayTimer ??= DispatcherQueue.CreateTimer();
        _todayTimer.Interval = TimeSpan.FromSeconds(30);
        _todayTimer.Tick -= TodayTick;
        _todayTimer.Tick += TodayTick;
        _todayTimer.Start();
        NotifyRange();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        ObserveOptions(null);
        Unsubscribe();
        _allItemsDirty = _dataDirty = true;
        _todayTimer?.Stop();
        if (_todayTimer is not null) _todayTimer.Tick -= TodayTick;
    }
    private void TodayTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        if (_today == CurrentDate()) return;
        _today = CurrentDate();
        RefreshDateStyles();
    }
    private DateOnly CurrentDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DisplayTimeZone).DateTime);

    /// <summary>Synchronously re-evaluates all models, or defers that work until the outermost DeferRefresh scope is disposed.</summary>
    /// <remarks>Use for models without change notifications. Call on the UI thread; invalid data is reported to the caller.</remarks>
    public void RefreshEvents()
    {
        VerifyDataAccess();
        _dataDirty = _allItemsDirty = true;
        if (_deferRefresh == 0) ProcessData();
    }

    /// <summary>Defers event-data processing until the outermost scope is disposed on the UI thread.</summary>
    /// <returns>An idempotent, nestable refresh scope.</returns>
    /// <remarks>Do not keep a scope across await. Property and collection changes in a scope are processed together.</remarks>
    public IDisposable DeferRefresh()
    {
        VerifyDataAccess();
        _deferRefresh++;
        return new RefreshScope(this);
    }

    private void Unsubscribe()
    {
        if (_collection is not null) _collection.CollectionChanged -= CollectionChanged;
        _collection = null;
        foreach (var item in _observed) item.PropertyChanged -= ItemChanged;
        _observed.Clear();
        _subscriptionsActive = false;
    }
    private MonthGrid? _grid;
    private MonthGrid CreateGrid()
    {
        var date = ToDate(DisplayDate);
        if (_grid is null || _grid.Range.PrimaryRange.Start.Year != date.Year || _grid.Range.PrimaryRange.Start.Month != date.Month || _gridFirstDay != FirstDayOfWeek)
        {
            _grid = MonthGrid.Create(date, FirstDayOfWeek);
            _gridFirstDay = FirstDayOfWeek;
        }
        return _grid;
    }
    private DayOfWeek _gridFirstDay;
    private void NotifyRange()
    {
        if (!_loaded) return;
        var next = CreateGrid().Range;
        if (_lastRange == next) return;
        var old = _lastRange;
        _lastRange = next;
        VisibleRangeChanged?.Invoke(this, new(old, next));
    }
    private void QueueRender()
    {
        if (_pending || _root is null) return;
        _pending = true;
        if (!DispatcherQueue.TryEnqueue(() => { _pending = false; Render(); })) _pending = false;
    }

    private void RefreshEventStyles(CalendarOccurrence? oldEvent = null, CalendarOccurrence? newEvent = null)
    {
        foreach (var visual in _eventPresenters.Values)
        {
            var occurrence = visual.Segment.Occurrence;
            if ((oldEvent is not null || newEvent is not null) && !SameOccurrence(occurrence, oldEvent) && !SameOccurrence(occurrence, newEvent)) continue;
            var selected = IsEventSelectionEnabled && SameOccurrence(occurrence, SelectedEvent);
            visual.Context.SetSelected(selected);
            visual.Outline.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetHelpText(visual.Control, selected ? GetString("Selected") : string.Empty);
        }
    }
    private static bool SameOccurrence(CalendarOccurrence? left, CalendarOccurrence? right) =>
        ReferenceEquals(left, right) || (left is not null && right is not null &&
            (left.Key is not null && right.Key is not null ? Equals(left.Key, right.Key) : ReferenceEquals(left.Source, right.Source)));

    private void RefreshDateSelection(DateTimeOffset? value)
    {
        if (!value.HasValue || !_days.TryGetValue(ToDate(value.Value), out var button)) return;
        var date = ToDate(value.Value);
        var selected = SelectionMode == CalendarSelectionMode.Single && SelectedDate.HasValue && ToDate(SelectedDate.Value) == date;
        if (button.Content is CalendarDateContext context) context.SetSelected(selected);
        _dayOutlines[date].Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetHelpText(button, selected ? GetString("Selected") : string.Empty);
    }

    private void RefreshDateStyles()
    {
        if (_root is null) return;
        var primary = PrimaryRange;
        var today = CurrentDate();
        foreach (var (date, button) in _days)
        {
            var selected = SelectionMode == CalendarSelectionMode.Single && SelectedDate.HasValue && ToDate(SelectedDate.Value) == date;
            var outside = !primary.Contains(date);
            var styleKey = date < today
                ? (outside ? "CalendarAdjacentDayStyle" : "CalendarDayStyle")
                : (outside ? "CalendarAdjacentCurrentOrFutureDayStyle" : "CalendarCurrentOrFutureDayStyle");
            button.Style = GetPartStyle(styleKey);
            _dayOutlines[date].Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            button.ContentTemplate = DateHeaderTemplate ?? GetPartTemplate("DefaultDateTemplate");
            button.Content = new CalendarDateContext
            {
                Date = Transport(date),
                Culture = DisplayCulture,
                DayText = outside ? date.ToString("M'/'d", DisplayCulture) : date.Day.ToString(DisplayCulture),
                IsToday = date == today,
                IsSelected = selected, IsOutsidePrimaryRange = outside, TodayText = TodayText ?? GetString("Today")
            };
            AutomationProperties.SetName(button, $"{date.ToString("D", DisplayCulture)}{(date == today ? $", {TodayText ?? GetString("Today")}" : "")}");
            AutomationProperties.SetHelpText(button, selected ? GetString("Selected") : string.Empty);
        }
    }
    private void InvokeDate(DateOnly date, Button anchor)
    {
        var outside = !PrimaryRange.Contains(date);
        SetKeyboardDate(date);
        if (SelectionMode == CalendarSelectionMode.Single) SelectedDate = Transport(date);
        DateInvoked?.Invoke(this, new(Transport(date), outside, anchor));
    }
    private void DateKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateOnly focusedDate }) return;
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        var offset = e.Key switch { VirtualKey.Left => rtl ? 1 : -1, VirtualKey.Right => rtl ? -1 : 1, VirtualKey.Up => -7, VirtualKey.Down => 7, _ => 0 };
        if (offset == 0) return;
        e.Handled = true;
        if (SelectionMode != CalendarSelectionMode.Single) return;
        var date = SelectedDate.HasValue ? ToDate(SelectedDate.Value) : focusedDate;
        var nextDay = date.DayNumber + offset;
        if (nextDay < DateOnly.MinValue.DayNumber || nextDay > DateOnly.MaxValue.DayNumber) return;
        var next = DateOnly.FromDayNumber(nextDay);
        if (!_days.TryGetValue(next, out var button)) return;
        SetKeyboardDate(next);
        button.Focus(FocusState.Programmatic);
        SelectedDate = Transport(next);
    }
    private void SetKeyboardDate(DateOnly date)
    {
        if (_focusDate.HasValue && _days.TryGetValue(_focusDate.Value, out var previous)) previous.IsTabStop = false;
        _focusDate = date;
        if (_days.TryGetValue(date, out var next)) next.IsTabStop = true;
    }
    private Style GetPartStyle(string key) => (Style)_root!.Resources[key];
    private DataTemplate GetPartTemplate(string key) => (DataTemplate)_root!.Resources[key];
    private void SetId(Button button, string id) { AutomationProperties.SetAutomationId(button, id); _focusTargets[id] = button; }
    private static Grid SevenColumns()
    {
        var grid = new Grid();
        for (var i = 0; i < 7; i++) grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        return grid;
    }
    private static bool IsDescendant(DependencyObject child, DependencyObject parent)
    {
        for (DependencyObject? node = child; node is not null; node = VisualTreeHelper.GetParent(node)) if (ReferenceEquals(node, parent)) return true;
        return false;
    }
    private static bool IsNestedInput(DependencyObject child, Button parent)
    {
        for (DependencyObject? node = child; node is not null && !ReferenceEquals(node, parent); node = VisualTreeHelper.GetParent(node))
            if (node is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase or TextBox or ComboBox) return true;
        return false;
    }
    private static DateOnly ToDate(DateTimeOffset date) => DateOnly.FromDateTime(date.Date);
    private static DateTimeOffset Transport(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
