using Microsoft.Windows.ApplicationModel.Resources;
using System.Collections.Concurrent;

namespace Calendar.WinUI.Controls;

internal static class CalendarStrings
{
    private static readonly Lazy<ResourceManager> Manager = new(() => new ResourceManager());
    private static readonly ConcurrentDictionary<(string Name, string Language), string> Strings = new();
    public static string Get(string name, string language) => Strings.GetOrAdd((name, language), static key =>
    {
        var context = Manager.Value.CreateResourceContext();
        context.QualifierValues["Language"] = key.Language;
        var candidate = Manager.Value.MainResourceMap.TryGetValue($"Calendar.WinUI/Resources/{key.Name}", context);
        return candidate?.ValueAsString ?? throw new InvalidOperationException($"Missing Calendar.WinUI resource: {key.Name}.");
    });
}
