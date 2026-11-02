using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Calendar.WinUI.Controls;

public sealed partial class Calendar
{
    private readonly Dictionary<(Style Style, bool Interactive), Style> _containerStyles = new();
    private readonly HashSet<(Style Style, bool Interactive)> _activeContainerStyles = new();
    private readonly List<(Style Style, bool Interactive)> _retiredContainerStyles = new();
    private bool HasEventInteraction => IsEventSelectionEnabled || IsEventInvocationEnabled;

    private void ApplyEventStyle(EventVisual visual)
    {
        var builtIn = GetPartStyle(HasEventInteraction ? "CalendarEventButtonStyle" : "CalendarReadOnlyEventStyle");
        var additional = EventContainerStyleSelector?.SelectStyle(visual.Context, visual.Control) ?? EventContainerStyle;
        if (additional is null) { visual.Control.Style = builtIn; return; }
        var target = HasEventInteraction ? typeof(Button) : typeof(ContentControl);
        if (additional.TargetType is null || !additional.TargetType.IsAssignableFrom(target))
            throw new ArgumentException("Event container styles must target ContentControl, or Button when event interaction is enabled.");
        var key = (additional, HasEventInteraction);
        _activeContainerStyles.Add(key);
        if (!_containerStyles.TryGetValue(key, out var cached))
        {
            var merged = new Style(target) { BasedOn = builtIn };
            var setters = new Dictionary<DependencyProperty, Setter>();
            void Collect(Style style)
            {
                if (style.BasedOn is { } parent) Collect(parent);
                foreach (var setter in style.Setters.OfType<Setter>()) setters[setter.Property] = setter;
            }
            Collect(additional);
            foreach (var setter in setters.Values) merged.Setters.Add(setter);
            cached = merged;
            _containerStyles.Add(key, cached);
        }
        visual.Control.Style = cached;
    }

    private void PruneContainerStyles()
    {
        // A selector may return fresh Style instances. Keep only styles used by the current visuals.
        foreach (var key in _containerStyles.Keys)
            if (!_activeContainerStyles.Contains(key)) _retiredContainerStyles.Add(key);
        foreach (var key in _retiredContainerStyles) _containerStyles.Remove(key);
        _retiredContainerStyles.Clear();
        _activeContainerStyles.Clear();
    }
}
