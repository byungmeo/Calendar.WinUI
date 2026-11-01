using Calendar.Core.Dates;

namespace Calendar.Core.Views;

/// <summary>The focused period and actual displayed dates, independent of any particular view.</summary>
public sealed record CalendarViewRange(DateRange PrimaryRange, DateRange VisibleRange);

// Additional values are introduced only when their actual view is implemented.
/// <summary>The implemented calendar display units.</summary>
public enum CalendarDisplayUnit
{
    /// <summary>A month grid with an automatic five or six weeks.</summary>
    Month
}
/// <summary>Controls whether dates retain selection state.</summary>
public enum CalendarSelectionMode
{
    /// <summary>Dates remain invokable without updating selection.</summary>
    None,
    /// <summary>One date can be selected at a time.</summary>
    Single
}
