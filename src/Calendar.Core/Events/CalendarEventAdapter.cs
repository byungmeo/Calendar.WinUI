namespace Calendar.Core.Events;

/// <summary>Optionally supplies a stable, unique occurrence key independently of mutable event data.</summary>
public interface ICalendarEventKeyProvider
{
    /// <summary>Returns an immutable occurrence key, or null to use source reference identity.</summary>
    /// <remarks>Keys must be unique in the source. Recurring events need an instance identifier as well as a series identifier.</remarks>
    object? GetKey(object source);
}

/// <summary>Creates type-safe, delegate-based adapters for application models.</summary>
public static class CalendarEventAdapter
{
    /// <summary>Creates an adapter without requiring an additional application class.</summary>
    /// <typeparam name="T">The application model type.</typeparam>
    /// <param name="data">Maps a model to occurrence data.</param>
    /// <param name="text">Maps a model to its display title.</param>
    /// <param name="key">Optionally maps a model to a stable, unique occurrence key.</param>
    /// <returns>An adapter that preserves the original application objects.</returns>
    public static ICalendarEventAdapter Create<T>(Func<T, CalendarEventData> data, Func<T, string> text, Func<T, object?>? key = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(text);
        return new DelegateAdapter<T>(data, text, key);
    }

    private sealed class DelegateAdapter<T>(Func<T, CalendarEventData> data, Func<T, string> text, Func<T, object?>? key)
        : ICalendarEventAdapter, ICalendarEventKeyProvider where T : class
    {
        private static T Model(object source) => source as T
            ?? throw new ArgumentException($"Expected an event of type {typeof(T).FullName}.", nameof(source));
        public CalendarEventData GetEvent(object source) => data(Model(source));
        public string GetDisplayText(object source) => text(Model(source));
        public object? GetKey(object source) => key?.Invoke(Model(source));
    }
}
