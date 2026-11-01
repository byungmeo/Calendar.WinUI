using Microsoft.UI.Xaml;

namespace Calendar.WinUI.Controls;

/// <summary>Month-only sizing; no layout options leak into the common event model.</summary>
public sealed partial class MonthViewOptions : DependencyObject
{
    /// <summary>Identifies the <see cref="EventRowHeight"/> dependency property.</summary>
    public static readonly DependencyProperty EventRowHeightProperty = DependencyProperty.Register(
        nameof(EventRowHeight), typeof(double), typeof(MonthViewOptions), new PropertyMetadata(24d, Changed));
    /// <summary>Identifies the <see cref="DateHeaderHeight"/> dependency property.</summary>
    public static readonly DependencyProperty DateHeaderHeightProperty = DependencyProperty.Register(
        nameof(DateHeaderHeight), typeof(double), typeof(MonthViewOptions), new PropertyMetadata(28d, Changed));
    /// <summary>Gets or sets event-row height in DIPs. The default is 24; the value must be finite and at least 20.</summary>
    public double EventRowHeight { get => (double)GetValue(EventRowHeightProperty); set => SetValue(EventRowHeightProperty, value); }
    /// <summary>Gets or sets date-header height in DIPs. The default is 28; the value must be finite and at least 20.</summary>
    public double DateHeaderHeight { get => (double)GetValue(DateHeaderHeightProperty); set => SetValue(DateHeaderHeightProperty, value); }
    internal event EventHandler? OptionsChanged;
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not double value || !double.IsFinite(value) || value < 20)
            throw new ArgumentOutOfRangeException(e.Property.ToString(), "Row heights must be finite and at least 20 DIPs.");
        ((MonthViewOptions)sender).OptionsChanged?.Invoke(sender, EventArgs.Empty);
    }
}
