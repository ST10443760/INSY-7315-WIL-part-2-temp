using ThriveWellness.Services;

namespace ThriveWellness.Tests;

public class ScheduleWeekTests
{
    // Friday 30 October 2026 - its week runs Mon 26 Oct to Sun 1 Nov.
    private static readonly DateTime Today = new(2026, 10, 30);
    private static readonly DateTime ThisMonday = new(2026, 10, 26);

    [Theory]
    [InlineData("2026-10-26", "2026-10-26")] // Monday stays put
    [InlineData("2026-10-27", "2026-10-26")] // Tuesday
    [InlineData("2026-10-31", "2026-10-26")] // Saturday
    [InlineData("2026-11-01", "2026-10-26")] // Sunday belongs to the week before, not the next
    [InlineData("2026-11-02", "2026-11-02")] // the following Monday starts a new week
    [InlineData("2027-01-01", "2026-12-28")] // across a year boundary
    public void StartOfWeek_SnapsToItsMonday(string date, string expectedMonday)
    {
        Assert.Equal(DateTime.Parse(expectedMonday), ScheduleWeek.StartOfWeek(DateTime.Parse(date)));
    }

    [Fact]
    public void StartOfWeek_IgnoresTimeOfDay()
    {
        Assert.Equal(ThisMonday, ScheduleWeek.StartOfWeek(new DateTime(2026, 11, 1, 23, 59, 59)));
    }

    [Fact]
    public void Resolve_NoWeekRequested_DefaultsToTheCurrentWeek()
    {
        var week = ScheduleWeek.Resolve(null, Today, new DateTime(2026, 11, 20));

        Assert.Equal(ThisMonday, week.Start);
        Assert.Equal(new DateTime(2026, 11, 1), week.End);
        Assert.True(week.IsThisWeek);
    }

    [Fact]
    public void Resolve_NonMondayRequest_SnapsToItsMonday()
    {
        var week = ScheduleWeek.Resolve(new DateTime(2026, 11, 12), Today, new DateTime(2026, 11, 20));

        Assert.Equal(new DateTime(2026, 11, 9), week.Start);
        Assert.False(week.IsThisWeek);
    }

    [Fact]
    public void Resolve_PastWeek_IsClampedToTheCurrentWeek()
    {
        var week = ScheduleWeek.Resolve(new DateTime(2026, 9, 1), Today, new DateTime(2026, 11, 20));

        Assert.Equal(ThisMonday, week.Start);
        Assert.Null(week.PreviousStart);
    }

    [Fact]
    public void Resolve_WeekAfterTheLastSession_IsClampedToTheLastSessionsWeek()
    {
        var week = ScheduleWeek.Resolve(new DateTime(2027, 3, 1), Today, new DateTime(2026, 11, 20));

        Assert.Equal(new DateTime(2026, 11, 16), week.Start);
        Assert.Null(week.NextStart);
    }

    [Fact]
    public void Resolve_NoUpcomingSessions_StaysOnTheCurrentWeekWithNowhereToGo()
    {
        var week = ScheduleWeek.Resolve(new DateTime(2026, 11, 16), Today, lastSessionDate: null);

        Assert.Equal(ThisMonday, week.Start);
        Assert.Null(week.PreviousStart);
        Assert.Null(week.NextStart);
    }

    [Fact]
    public void Resolve_LastSessionInThePast_StaysOnTheCurrentWeek()
    {
        var week = ScheduleWeek.Resolve(null, Today, new DateTime(2026, 10, 1));

        Assert.Equal(ThisMonday, week.Start);
        Assert.Null(week.NextStart);
    }

    [Fact]
    public void Resolve_WeekInTheMiddle_HasBothPreviousAndNext()
    {
        var week = ScheduleWeek.Resolve(new DateTime(2026, 11, 9), Today, new DateTime(2026, 11, 20));

        Assert.Equal(new DateTime(2026, 11, 2), week.PreviousStart);
        Assert.Equal(new DateTime(2026, 11, 16), week.NextStart);
    }

    [Fact]
    public void Resolve_LastSessionOnASunday_StillReachesThatWeek()
    {
        // Sunday 8 Nov belongs to the week starting Monday 2 Nov.
        var week = ScheduleWeek.Resolve(null, Today, new DateTime(2026, 11, 8));

        Assert.Equal(new DateTime(2026, 11, 2), week.NextStart);

        var lastWeek = ScheduleWeek.Resolve(week.NextStart, Today, new DateTime(2026, 11, 8));
        Assert.Null(lastWeek.NextStart);
    }

    [Theory]
    [InlineData("2026-11-02", "2 - 8 November 2026")]
    [InlineData("2026-10-26", "26 October - 1 November 2026")]
    [InlineData("2026-12-28", "28 December 2026 - 3 January 2027")]
    public void RangeLabel_ReadsNaturally(string monday, string expected)
    {
        var week = new ScheduleWeek(DateTime.Parse(monday), null, null, false);

        Assert.Equal(expected, week.RangeLabel());
    }
}
