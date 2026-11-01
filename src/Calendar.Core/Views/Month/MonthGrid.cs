using Calendar.Core.Dates;

namespace Calendar.Core.Views.Month;

/// <summary>A date cell and whether it belongs to an adjacent month.</summary>
public sealed record MonthDay(DateOnly Date, bool IsOutsidePrimaryRange);

/// <summary>A month grid containing five or six weeks of calendar dates.</summary>
public sealed class MonthGrid
{
    private MonthGrid(CalendarViewRange range, IReadOnlyList<MonthDay> days) { Range = range; Days = days; }
    /// <summary>Gets the primary month and complete visible range.</summary>
    public CalendarViewRange Range { get; }
    /// <summary>Gets date cells in chronological order.</summary>
    public IReadOnlyList<MonthDay> Days { get; }
    /// <summary>Gets the automatic number of week rows: five or six.</summary>
    public int WeekCount => Days.Count / 7;

    /// <summary>Creates a month grid for the supplied date and first weekday. Display years 2 through 9998 are supported.</summary>
    public static MonthGrid Create(DateOnly displayDate, DayOfWeek firstDayOfWeek)
    {
        // Leave room for adjacent dates at both DateOnly boundaries.
        if (displayDate.Year is < 2 or > 9998)
            throw new ArgumentOutOfRangeException(nameof(displayDate), "Display years 2 through 9998 are supported.");
        if (!Enum.IsDefined(firstDayOfWeek)) throw new ArgumentOutOfRangeException(nameof(firstDayOfWeek));
        var first = new DateOnly(displayDate.Year, displayDate.Month, 1);
        var primary = new DateRange(first, first.AddMonths(1));
        var start = first.AddDays(-((7 + (int)first.DayOfWeek - (int)firstDayOfWeek) % 7));
        var requiredDays = first.DayNumber - start.DayNumber + primary.DayCount;
        var weekCount = Math.Max(5, (requiredDays + 6) / 7);
        var dayCount = weekCount * 7;
        var visible = new DateRange(start, start.AddDays(dayCount));
        var days = Enumerable.Range(0, dayCount).Select(i => start.AddDays(i))
            .Select(date => new MonthDay(date, !primary.Contains(date))).ToArray();
        return new(new(primary, visible), Array.AsReadOnly(days));
    }
}
