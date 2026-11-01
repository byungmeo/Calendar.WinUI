using Calendar.Core.Dates;
using Calendar.Core.Events;

namespace Calendar.Core.Views.Month;

/// <summary>One week-clipped event bar with a zero-based start column and lane.</summary>
public sealed record MonthSegment(CalendarOccurrence Occurrence, DateRange Dates, int StartColumn, int ColumnSpan, int Lane)
{
    /// <summary>Gets whether the original occurrence begins before this segment.</summary>
    public bool ContinuesBefore => Occurrence.Dates.Start < Dates.Start;
    /// <summary>Gets whether the original occurrence continues after this segment.</summary>
    public bool ContinuesAfter => Occurrence.Dates.EndExclusive > Dates.EndExclusive;
}

/// <summary>The complete ordered occurrence list and hidden count for one date.</summary>
public sealed record MonthDayEvents(DateOnly Date, IReadOnlyList<CalendarOccurrence> Events, int HiddenCount);
/// <summary>The displayed event segments and per-date overflow information for one week.</summary>
public sealed record MonthWeek(DateRange Dates, IReadOnlyList<MonthSegment> Segments, IReadOnlyList<MonthDayEvents> Days, bool HasOverflow);
/// <summary>The computed layout for a month grid.</summary>
public sealed record MonthLayout(MonthGrid Grid, IReadOnlyList<MonthWeek> Weeks);

/// <summary>Pure month projection. Capacity is measured by the renderer; no pixels or XAML enter the core.</summary>
public static class MonthLayoutEngine
{
    /// <summary>Builds the visible month segments and overflow lists for a positive event-row capacity.</summary>
    public static MonthLayout Build(MonthGrid grid, IEnumerable<CalendarOccurrence> occurrences, int rowCapacity)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(occurrences);
        if (rowCapacity < 1) throw new ArgumentOutOfRangeException(nameof(rowCapacity));
        var sorted = CalendarOccurrence.Sort(occurrences.Where(x => x.Dates.Overlaps(grid.Range.VisibleRange)));
        var weeks = new List<MonthWeek>(grid.WeekCount);
        for (var week = 0; week < grid.WeekCount; week++)
        {
            var start = grid.Range.VisibleRange.Start.AddDays(week * 7);
            var range = new DateRange(start, start.AddDays(7));
            var segments = new List<MonthSegment>();
            var lastLaneByColumn = Enumerable.Repeat(-1, 7).ToArray();
            foreach (var occurrence in sorted)
            {
                if (occurrence.Dates.Intersect(range) is not { } dates) continue;
                // Preserve priority on every shared day, even when it leaves a hole.
                var column = dates.Start.DayNumber - start.DayNumber;
                var lane = 0;
                for (var day = column; day < column + dates.DayCount; day++) lane = Math.Max(lane, lastLaneByColumn[day] + 1);
                for (var day = column; day < column + dates.DayCount; day++) lastLaneByColumn[day] = lane;
                segments.Add(new(occurrence, dates, column, dates.DayCount, lane));
            }
            var overflow = segments.Any(s => s.Lane >= rowCapacity);
            var capacity = overflow ? rowCapacity - 1 : rowCapacity;
            var shown = segments.Where(s => s.Lane < capacity).ToArray();
            var days = Enumerable.Range(0, 7).Select(column =>
            {
                var date = start.AddDays(column);
                var all = segments.Where(s => s.Dates.Contains(date)).Select(s => s.Occurrence).ToArray();
                var count = shown.Count(s => s.Dates.Contains(date));
                return new MonthDayEvents(date, Array.AsReadOnly(all), all.Length - count);
            }).ToArray();
            weeks.Add(new(range, Array.AsReadOnly(shown), Array.AsReadOnly(days), overflow));
        }
        return new(grid, weeks.AsReadOnly());
    }
}
