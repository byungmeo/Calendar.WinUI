# Templates

Replace content using `DataTemplate` while retaining the control's layout and input handling.

| Property | Data context |
| --- | --- |
| `EventTemplate` | <xref:Calendar.WinUI.Presentation.CalendarEventContext> |
| `DateHeaderTemplate` | <xref:Calendar.WinUI.Presentation.CalendarDateContext> |
| `DayOfWeekHeaderTemplate` | <xref:Calendar.WinUI.Presentation.CalendarDayOfWeekContext> |

## Event content

```xml
<calendar:Calendar x:Name="Schedule">
    <calendar:Calendar.EventTemplate>
        <DataTemplate>
            <Grid ColumnSpacing="4">
                <TextBlock
                    Text="{Binding Label}"
                    VerticalAlignment="Center"
                    TextTrimming="CharacterEllipsis" />
            </Grid>
        </DataTemplate>
    </calendar:Calendar.EventTemplate>
</calendar:Calendar>
```

`Label` includes continuation markers at week boundaries. `Title` contains the original display title. The context also exposes `Occurrence`, `Source`, `DisplayedDates`, `ContinuesBefore`, `ContinuesAfter`, and `IsSelected`.

A visible segment can cover fewer dates than the original event. Read `Occurrence.Data` or the application `Source` for original values.

An application can place an independent button inside its template. Nested button, text-input, and ComboBox interactions are not invoked again as parent event actions. Disabling parent selection or invocation does not disable these independent inputs.

## Event container appearance

Use <xref:Calendar.WinUI.Controls.Calendar.EventContainerStyle> to change the outer event bar while retaining the built-in template and input handling:

```xml
<calendar:Calendar>
    <calendar:Calendar.EventContainerStyle>
        <Style TargetType="ContentControl">
            <Setter Property="Padding" Value="8,0" />
            <Setter Property="Margin" Value="2,1" />
            <Setter Property="Background" Value="{ThemeResource AccentFillColorDefaultBrush}" />
        </Style>
    </calendar:Calendar.EventContainerStyle>
</calendar:Calendar>
```

The container's `DataContext` is `CalendarEventContext`. Use `ContentControl` as the style target to support both interactive buttons and display-only containers. Additional setters, including setters inherited through `BasedOn`, override built-in defaults; setting `Template` explicitly replaces that template. WinUI does not support data bindings in style setter values. Use a style selector for data-dependent container appearance, and keep ordinary content bindings inside data templates.

For per-event decisions without runtime property binding, assign <xref:Calendar.WinUI.Controls.Calendar.EventContainerStyleSelector>. Its `SelectStyleCore` receives a `CalendarEventContext` and the current container. A null result falls back to `EventContainerStyle`. Styles are cached after composition; replace the property to apply a new style rather than modifying a style after use.

```csharp
sealed class AppointmentStyleSelector : StyleSelector
{
    public required Style WorkStyle { get; init; }
    public required Style PersonalStyle { get; init; }

    protected override Style SelectStyleCore(object item, DependencyObject container)
    {
        var context = (CalendarEventContext)item;
        return context.Source is Appointment { IsWork: true }
            ? WorkStyle : PersonalStyle;
    }
}
```

The normal background can vary by item. Hover, pressed, and selection colors use the theme resource keys `CalendarEventPointerOverBrush`, `CalendarEventPressedBrush`, and `CalendarEventSelectionBorderBrush`. Override these keys in the application's theme dictionaries when needed; content templates and container styles do not replace the separate selection outline.

## Date and weekday content

Date contexts expose `DayText`, `IsToday`, `IsSelected`, `IsOutsidePrimaryRange`, and the today label. `Date` is a date-only transport value.

Weekday contexts expose `DayOfWeek` and localized `Text`. Header order still follows `FirstDayOfWeek`, independently of the template or display language.

Assign null to restore a default template. These properties replace content areas, rather than the entire date-cell layout or selection outline.
