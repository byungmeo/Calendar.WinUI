using Calendar.Core.Dates;
using Calendar.Core.Events;
using Calendar.Core.Views.Month;
using Xunit;

namespace Calendar.Core.Tests;

public class CalendarTests
{
    private static readonly DateOnly D = new(2026, 10, 28);
    private static CalendarOccurrence Project(CalendarEventData data, int index = 0, object? source = null,
        TimeZoneInfo? zone = null) => CalendarOccurrence.Project(source ?? new object(), data, "Event", index, zone ?? TimeZoneInfo.Utc);

    [Fact]
    public void MonthGridHasFiveOrSixCompleteConsecutiveWeeks()
    {
        foreach (var year in new[] { 2024, 2026, 9998 })
        foreach (var month in Enumerable.Range(1, 12))
        foreach (var first in Enum.GetValues<DayOfWeek>())
        {
            var grid = MonthGrid.Create(new(year, month, 15), first);
            Assert.InRange(grid.WeekCount, 5, 6);
            Assert.Equal(grid.WeekCount * 7, grid.Days.Count);
            Assert.Equal(first, grid.Days[0].Date.DayOfWeek);
            Assert.Equal(new DateOnly(year, month, 1), grid.Range.PrimaryRange.Start);
            Assert.Equal(DateTime.DaysInMonth(year, month), grid.Days.Count(x => !x.IsOutsidePrimaryRange));
            for (var i = 1; i < grid.Days.Count; i++) Assert.Equal(grid.Days[i - 1].Date.AddDays(1), grid.Days[i].Date);
            Assert.True(grid.Range.VisibleRange.Contains(grid.Range.PrimaryRange.EndExclusive.AddDays(-1)));
        }
    }

    [Theory]
    [InlineData(2026, 10, DayOfWeek.Sunday, 5)]
    [InlineData(2026, 8, DayOfWeek.Sunday, 6)]
    [InlineData(2026, 2, DayOfWeek.Sunday, 5)]
    [InlineData(2026, 3, DayOfWeek.Monday, 6)]
    [InlineData(2026, 3, DayOfWeek.Sunday, 5)]
    public void WeekCountReflectsMonthAndFirstDay(int year, int month, DayOfWeek first, int expected) =>
        Assert.Equal(expected, MonthGrid.Create(new(year, month, 1), first).WeekCount);

    [Fact]
    public void NavigatingMonthsRecalculatesVisibleRange()
    {
        var october = MonthGrid.Create(D, DayOfWeek.Sunday);
        var august = MonthGrid.Create(D.AddMonths(-2), DayOfWeek.Sunday);
        var september = MonthGrid.Create(D.AddMonths(-1), DayOfWeek.Sunday);
        Assert.Equal(35, october.Range.VisibleRange.DayCount);
        Assert.Equal(42, august.Range.VisibleRange.DayCount);
        Assert.Equal(35, september.Range.VisibleRange.DayCount);
        Assert.Equal(new DateOnly(2026, 9, 6), august.Range.VisibleRange.EndExclusive);
        Assert.Equal(new DateOnly(2026, 10, 4), september.Range.VisibleRange.EndExclusive);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9999)]
    public void DisplayBoundaryIsRejectedExplicitly(int year) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MonthGrid.Create(new(year, 1, 1), DayOfWeek.Sunday));

    [Fact]
    public void ArbitraryDayAndWeekRangesDoNotDependOnMonth()
    {
        var occurrence = Project(CalendarEventData.AllDay(D, D.AddDays(3)));
        Assert.True(occurrence.Dates.Overlaps(new(D.AddDays(2), D.AddDays(3))));
        Assert.False(occurrence.Dates.Overlaps(new(D.AddDays(3), D.AddDays(10))));
        Assert.Equal(2, occurrence.Dates.Intersect(new(D.AddDays(1), D.AddDays(8)))!.Value.DayCount);
    }

    [Fact]
    public void MissingAllDayEndMeansOneDay() => Assert.Equal(1, Project(CalendarEventData.AllDay(D)).Dates.DayCount);

    [Fact]
    public void MidnightEndDoesNotIncludeFollowingDate()
    {
        var e = Project(CalendarEventData.Timed(new(2026, 10, 28, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateRange(D, D.AddDays(1)), e.Dates);
    }

    [Fact]
    public void PointEventIsNotAnEmptyOrArtificiallyExtendedInterval()
    {
        var e = Project(CalendarEventData.Timed(new(2026, 10, 28, 23, 0, 0, TimeSpan.Zero)));
        Assert.True(e.Data.IsInstant);
        Assert.Null(e.Data.End);
        Assert.Equal(new DateRange(D, D.AddDays(1)), e.Dates);
    }

    [Fact]
    public void ExplicitZeroAndNegativeDurationAreInvalid()
    {
        var start = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarEventData.Timed(start, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarEventData.Timed(start, start.AddMinutes(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarEventData.AllDay(D, D));
    }

    [Fact]
    public void AbsoluteTimeUsesDisplayZoneWhileFloatingAndAllDayDoNotMove()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+9", TimeSpan.FromHours(9), "UTC+9", "UTC+9");
        var absolute = Project(CalendarEventData.Timed(new(2026, 10, 28, 23, 0, 0, TimeSpan.Zero)), zone: zone);
        Assert.Equal(D.AddDays(1), absolute.Dates.Start);
        var floating = Project(CalendarEventData.Floating(new(2026, 10, 28, 23, 0, 0)), zone: zone);
        Assert.Equal(D, floating.Dates.Start);
        Assert.Equal(D, Project(CalendarEventData.AllDay(D), zone: zone).Dates.Start);
    }

    [Fact]
    public void DaylightSavingFallBackKeepsValidAbsoluteInterval()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var data = CalendarEventData.Timed(new(2026, 11, 1, 5, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 1, 6, 15, 0, TimeSpan.Zero));
        Assert.Equal(1, Project(data, zone: zone).Dates.DayCount);
        Assert.Equal(TimeSpan.FromMinutes(45), data.End!.Value - data.Start!.Value);
    }

    [Fact]
    public void FloatingRejectsImplicitUtcConversion() => Assert.Throws<ArgumentException>(() =>
        CalendarEventData.Floating(DateTime.SpecifyKind(new(2026, 10, 28), DateTimeKind.Utc)));

    [Fact]
    public void SortPreservesAllDayInputOrderAndTimedStartOrder()
    {
        var timedLate = Project(CalendarEventData.Floating(new(2026, 10, 28, 12, 0, 0)), 0);
        var allDayLate = Project(CalendarEventData.AllDay(D.AddDays(2)), 1);
        var allDayEarly = Project(CalendarEventData.AllDay(D), 2);
        var timedEarly = Project(CalendarEventData.Floating(new(2026, 10, 28, 9, 0, 0)), 3);
        var tied = Project(timedEarly.Data, 4);
        Assert.Equal(new[] { allDayLate, allDayEarly, timedEarly, tied, timedLate },
            CalendarOccurrence.Sort(new[] { timedLate, allDayLate, allDayEarly, timedEarly, tied }));
    }

    [Fact]
    public void WeekSegmentsRetainOriginalSourceAndContinuation()
    {
        var source = new object();
        var occurrence = Project(CalendarEventData.AllDay(new(2026, 10, 21), new DateOnly(2026, 10, 28)), source: source);
        var layout = MonthLayoutEngine.Build(MonthGrid.Create(D, DayOfWeek.Sunday), new[] { occurrence }, 4);
        var pieces = layout.Weeks.SelectMany(w => w.Segments).ToArray();
        Assert.Equal(2, pieces.Length);
        Assert.All(pieces, piece => Assert.Same(source, piece.Occurrence.Source));
        Assert.False(pieces[0].ContinuesBefore);
        Assert.True(pieces[0].ContinuesAfter);
        Assert.True(pieces[1].ContinuesBefore);
        Assert.False(pieces[1].ContinuesAfter);
        Assert.Equal(7, pieces.Sum(p => p.ColumnSpan));
    }

    [Fact]
    public void MonthBoundaryDoesNotSplitWithinSameWeek()
    {
        var e = Project(CalendarEventData.AllDay(new(2026, 10, 30), new DateOnly(2026, 11, 3)));
        var grid = MonthGrid.Create(D, DayOfWeek.Monday);
        var pieces = MonthLayoutEngine.Build(grid, new[] { e }, 4).Weeks.SelectMany(x => x.Segments).ToArray();
        Assert.Equal(new DateOnly(2026, 11, 2), pieces[0].Dates.EndExclusive);
    }

    [Fact]
    public void ClippingIncludesEventsThatStartedBeforeVisibleRange()
    {
        var grid = MonthGrid.Create(D, DayOfWeek.Sunday);
        var e = Project(CalendarEventData.AllDay(grid.Range.VisibleRange.Start.AddDays(-10), grid.Range.VisibleRange.EndExclusive.AddDays(10)));
        var pieces = MonthLayoutEngine.Build(grid, new[] { e }, 3).Weeks.SelectMany(x => x.Segments).ToArray();
        Assert.Equal(grid.WeekCount, pieces.Length);
        Assert.All(pieces, p => { Assert.True(p.ContinuesBefore); Assert.True(p.ContinuesAfter); });
    }

    [Fact]
    public void LowerPriorityCannotFillHoleAboveOverlappingHigherPriority()
    {
        var monday = new DateOnly(2026, 10, 26);
        var a = Project(CalendarEventData.AllDay(monday), 0);
        var b = Project(CalendarEventData.AllDay(monday, monday.AddDays(3)), 1);
        var c = Project(CalendarEventData.AllDay(monday.AddDays(1)), 2);
        var pieces = MonthLayoutEngine.Build(MonthGrid.Create(D, DayOfWeek.Monday), new[] { a, b, c }, 5)
            .Weeks.SelectMany(x => x.Segments).ToArray();
        Assert.Equal(new[] { 0, 1, 2 }, pieces.Select(p => p.Lane));
    }

    [Fact]
    public void OverflowReservesLastSlotAndReturnsWholeSortedList()
    {
        var entries = Enumerable.Range(0, 7).Select(i => Project(CalendarEventData.AllDay(D), i)).ToArray();
        var week = MonthLayoutEngine.Build(MonthGrid.Create(D, DayOfWeek.Sunday), entries, 3).Weeks.Single(w => w.Dates.Contains(D));
        Assert.True(week.HasOverflow);
        Assert.Equal(2, week.Segments.Count);
        var day = week.Days.Single(x => x.Date == D);
        Assert.Equal(5, day.HiddenCount);
        Assert.Equal(entries, day.Events);
    }

    [Fact]
    public void OverflowHidesWholeSegmentAcrossWeek()
    {
        var a = Project(CalendarEventData.AllDay(D), 0);
        var b = Project(CalendarEventData.AllDay(D, D.AddDays(3)), 1);
        var c = Project(CalendarEventData.AllDay(D), 2);
        var week = MonthLayoutEngine.Build(MonthGrid.Create(D, DayOfWeek.Monday), new[] { a, b, c }, 2).Weeks.Single(w => w.Dates.Contains(D));
        Assert.Single(week.Segments);
        Assert.Equal(1, week.Days.Single(x => x.Date == D.AddDays(1)).HiddenCount);
    }

    [Fact]
    public void SingleSlotShowsOnlyMoreWhenOverflowing()
    {
        var entries = Enumerable.Range(0, 2).Select(i => Project(CalendarEventData.AllDay(D), i));
        var week = MonthLayoutEngine.Build(MonthGrid.Create(D, DayOfWeek.Sunday), entries, 1).Weeks.Single(w => w.Dates.Contains(D));
        Assert.Empty(week.Segments);
        Assert.Equal(2, week.Days.Single(x => x.Date == D).HiddenCount);
    }

    [Fact]
    public void SharedUidDoesNotMergeOccurrencesOrRepeatedSourceEntries()
    {
        var source = new object();
        var data = CalendarEventData.AllDay(D) with { Uid = "series", RecurrenceId = "first" };
        var first = Project(data, 0, source);
        var second = Project(data with { RecurrenceId = "second" }, 1, source);
        var day = MonthLayoutEngine.Build(MonthGrid.Create(D, DayOfWeek.Monday), new[] { first, second }, 5)
            .Weeks.SelectMany(w => w.Days).Single(x => x.Date == D);
        Assert.Equal(2, day.Events.Count);
        Assert.Equal(new[] { "first", "second" }, day.Events.Select(x => x.Data.RecurrenceId));
    }

    [Fact]
    public void DelegateAdapterMapsTypedModelsAndStableOccurrenceKeys()
    {
        var item = new Appointment("series", "instance", "Meeting", D);
        var adapter = CalendarEventAdapter.Create<Appointment>(
            data: model => CalendarEventData.AllDay(model.Date),
            text: model => model.Title,
            key: model => (model.Id, model.Instance));
        Assert.Equal(new DateRange(D, D.AddDays(1)), adapter.GetEvent(item).AllDayDates);
        Assert.Equal("Meeting", adapter.GetDisplayText(item));
        var key = Assert.IsAssignableFrom<ICalendarEventKeyProvider>(adapter).GetKey(item);
        var occurrence = CalendarOccurrence.Project(item, adapter.GetEvent(item), adapter.GetDisplayText(item), 0, TimeZoneInfo.Utc, key);
        Assert.Same(item, occurrence.Source);
        Assert.Equal(("series", "instance"), occurrence.Key);
    }

    [Fact]
    public void DelegateAdapterWithoutKeyPreservesReferenceIdentityDefault()
    {
        var adapter = CalendarEventAdapter.Create<Appointment>(model => CalendarEventData.AllDay(model.Date), model => model.Title);
        Assert.Null(((ICalendarEventKeyProvider)adapter).GetKey(new Appointment("id", "instance", "Title", D)));
        Assert.Throws<ArgumentException>(() => adapter.GetEvent(new object()));
    }

    [Fact]
    public void DelegateAdapterRejectsMissingRequiredMappings()
    {
        Assert.Throws<ArgumentNullException>(() => CalendarEventAdapter.Create<Appointment>(null!, model => model.Title));
        Assert.Throws<ArgumentNullException>(() => CalendarEventAdapter.Create<Appointment>(model => CalendarEventData.AllDay(model.Date), null!));
    }

    private sealed record Appointment(string Id, string Instance, string Title, DateOnly Date);
}
