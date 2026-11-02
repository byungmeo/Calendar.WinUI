using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Calendar.Example;

public sealed partial class MainWindow
{
    private readonly CultureInfo _systemUiCulture = CultureInfo.CurrentUICulture;
    private CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private readonly ResourceManager _resources = new();
    private readonly Dictionary<(string Name, string Language), string> _strings = new();
    private bool _settingsReady;

    private string Text(string name, params object?[] values)
    {
        var key = (name, _uiCulture.Name);
        if (!_strings.TryGetValue(key, out var format))
        {
            var context = _resources.CreateResourceContext();
            context.QualifierValues["Language"] = _uiCulture.Name;
            format = _resources.MainResourceMap.GetValue($"Resources/{name}", context).ValueAsString;
            _strings.Add(key, format);
        }
        return string.Format(_uiCulture, format, values);
    }

    private static string OffsetText(TimeSpan offset) => $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm}";

    private void InitializeSettings()
    {
        var system = new ComboBoxItem();
        AutomationProperties.SetAutomationId(system, "TimeZoneSystem");
        TimeZonePicker.Items.Add(system);
        // Fixed offsets cover system zone offsets, including half-hour and quarter-hour zones.
        var offsets = TimeZoneInfo.GetSystemTimeZones().Select(zone => zone.BaseUtcOffset)
            .Append(TimeSpan.Zero).Distinct().OrderBy(offset => offset);
        foreach (var offset in offsets)
        {
            var label = OffsetText(offset);
            var zone = offset == TimeSpan.Zero ? TimeZoneInfo.Utc : TimeZoneInfo.CreateCustomTimeZone(label, offset, label, label);
            var item = new ComboBoxItem { Content = label, Tag = zone };
            AutomationProperties.SetAutomationId(item, $"TimeZoneUTC_{(offset < TimeSpan.Zero ? "M" : "P")}{offset.Duration():hh\\_mm}");
            TimeZonePicker.Items.Add(item);
        }
        TimeZonePicker.SelectedIndex = LanguagePicker.SelectedIndex = 0;
        _settingsReady = true;
        ApplyLanguage();
    }

    private void TimeZone_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsReady) return;
        if (TimeZonePicker.SelectedItem is ComboBoxItem { Tag: TimeZoneInfo zone }) Board.DisplayTimeZone = zone;
        else Board.ClearValue(global::Calendar.WinUI.Controls.Calendar.DisplayTimeZoneProperty);
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_settingsReady) ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        var language = (LanguagePicker.SelectedItem as ComboBoxItem)?.Tag as string;
        _uiCulture = string.IsNullOrEmpty(language) ? _systemUiCulture : CultureInfo.GetCultureInfo(language);
        Root.Language = _uiCulture.Name;
        Board.DisplayLanguage = string.IsNullOrEmpty(language) ? null : language;
        PreviousButton.Content = Text("Previous"); TodayButton.Content = Text("Today"); NextButton.Content = Text("Next");
        AddButton.Content = Text("Add"); ResetButton.Content = Text("Reset");
        CustomTemplateToggle.Header = Text("CustomTemplate"); DarkToggle.Header = Text("DarkTheme");
        SelectionToggle.Header = Text("DateSelection"); EventSelectionToggle.Header = Text("EventSelection");
        foreach (var toggle in new[] { CustomTemplateToggle, DarkToggle, SelectionToggle, EventSelectionToggle })
        {
            toggle.OnContent = Text("On"); toggle.OffContent = Text("Off");
        }
        TimeZonePicker.Header = Text("TimeZone"); LanguagePicker.Header = Text("Language");
        AutomationProperties.SetName(TimeZonePicker, Text("TimeZone")); AutomationProperties.SetName(LanguagePicker, Text("Language"));
        ((ComboBoxItem)TimeZonePicker.Items[0]).Content = $"{Text("System")} ({OffsetText(TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.UtcNow))})";
        ((ComboBoxItem)LanguagePicker.Items[0]).Content = Text("System");
        RefreshSystemChoice(TimeZonePicker);
        RefreshSystemChoice(LanguagePicker);
        RefreshDiagnostics(); UpdateTitle();
    }

    private void RefreshSystemChoice(ComboBox picker)
    {
        if (picker.SelectedIndex != 0) return;
        // Replace the system item so ComboBox also invalidates its cached selected-content presentation.
        var previous = (ComboBoxItem)picker.Items[0];
        var replacement = new ComboBoxItem { Content = previous.Content, Tag = previous.Tag };
        AutomationProperties.SetAutomationId(replacement, AutomationProperties.GetAutomationId(previous));
        _settingsReady = false;
        try { picker.Items[0] = replacement; picker.SelectedIndex = 0; }
        finally { _settingsReady = true; }
    }
}
