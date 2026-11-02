using System.Globalization;
using Calendar.Core.Events;

namespace Calendar.Example;

public sealed partial class MainWindow
{
    private void AddMonthEvents(DateOnly month)
    {
        // Keep the overlap demonstration in a complete week, clear of incoming month-boundary events.
        var fourth = month.AddDays(3);
        var week = fourth.AddDays(((int)Board.FirstDayOfWeek - (int)fourth.DayOfWeek + 7) % 7);

        AddAllDay("Product development", week.AddDays(1), week.AddDays(6));
        AddAllDay("Design review", week.AddDays(2), week.AddDays(5));
        AddAllDay("Usability testing", week.AddDays(3), week.AddDays(5));
        AddAllDay("Release rollout", week.AddDays(6), week.AddDays(10));

        var mixedDay = week.AddDays(10);
        AddAllDay("Team offsite", mixedDay);
        // Supply the afternoon event first so chronological sorting is visible in the sample.
        AddFloating("14:00 Product demo", mixedDay, new(14, 0), new(15, 0));
        AddFloating("09:00 Planning", mixedDay, new(9, 0), new(10, 0));
        _events.Add(new WorkItem("Custom application model", week.AddDays(11), week.AddDays(13)));
        _events.Add(new CalendarEvent("22:00 Overnight deployment", CalendarEventData.Floating(
            week.AddDays(12).ToDateTime(new(22, 0)), week.AddDays(13).ToDateTime(new(6, 0)))));
        _events.Add(new CalendarEvent("UTC check-in (23:30Z)", CalendarEventData.Timed(
            new DateTimeOffset(week.AddDays(8).ToDateTime(new(23, 30)), TimeSpan.Zero))));
        _events.Add(new CalendarEvent("11:00 Reminder (no end)", CalendarEventData.Floating(week.AddDays(8).ToDateTime(new(11, 0)))));
        AddFloating("14:00 Customer feedback and accessibility review with the product team", week.AddDays(13), new(14, 0), new(15, 0));

        // Isolate overflow near the start of the month so it does not hide the later showcase rows.
        var busyDay = month.AddDays(1);
        AddAllDay("Release sprint", busyDay.AddDays(-1), busyDay.AddDays(2));
        AddAllDay("Support rotation", busyDay.AddDays(1), busyDay.AddDays(3));
        AddAllDay("Launch day", busyDay);
        string[] meetings = ["Stand-up", "Customer call", "Design sync", "Planning", "Code review", "Product demo",
            "Team sync", "Support review", "Release check", "Partner call", "Retrospective", "Wrap-up"];
        for (var i = 0; i < meetings.Length; i++)
        {
            var start = new TimeOnly(9 + i / 2, i % 2 * 30);
            AddFloating($"{start.ToString("HH:mm", CultureInfo.InvariantCulture)} {meetings[i]}", busyDay, start, start.AddMinutes(25));
        }

        var nextMonth = month.AddMonths(1);
        AddAllDay("Month-end close", nextMonth.AddDays(-3), nextMonth.AddDays(2));
        AddAllDay("Monthly reporting", nextMonth.AddDays(-1), nextMonth.AddDays(3));
    }

    private void AddAllDay(string title, DateOnly start, DateOnly? endExclusive = null) =>
        _events.Add(new CalendarEvent(title, CalendarEventData.AllDay(start, endExclusive)));

    private void AddFloating(string title, DateOnly date, TimeOnly start, TimeOnly end) =>
        _events.Add(new CalendarEvent(title, CalendarEventData.Floating(date.ToDateTime(start), date.ToDateTime(end))));
}
