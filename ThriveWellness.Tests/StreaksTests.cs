using ThriveWellness.Services;

namespace ThriveWellness.Tests;

public class StreaksTests
{
    // Wednesday 4 November 2026; its week runs Monday 2 to Sunday 8.
    private static readonly DateTime Today = new(2026, 11, 4);

    // A date in the week n weeks before this one (0 = this week's Monday).
    private static DateTime WeeksAgo(int n, int dayOffset = 0) => new DateTime(2026, 11, 2).AddDays(-7 * n + dayOffset);

    [Fact]
    public void NoClasses_HasNoStreak()
    {
        var result = Streaks.Calculate(Array.Empty<DateTime>(), Today);

        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Longest);
        Assert.False(result.AttendedThisWeek);
    }

    [Fact]
    public void ClassesInThisWeekAndTheTwoBefore_IsAThreeWeekStreak()
    {
        var result = Streaks.Calculate(new[] { WeeksAgo(0), WeeksAgo(1), WeeksAgo(2) }, Today);

        Assert.Equal(3, result.Current);
        Assert.Equal(3, result.Longest);
        Assert.True(result.AttendedThisWeek);
    }

    [Fact]
    public void NothingYetThisWeek_StreakCarriesOnFromLastWeek()
    {
        var result = Streaks.Calculate(new[] { WeeksAgo(1), WeeksAgo(2) }, Today);

        Assert.Equal(2, result.Current);
        Assert.False(result.AttendedThisWeek);
    }

    [Fact]
    public void AWholeWeekMissed_ResetsTheCurrentStreakButKeepsTheLongest()
    {
        var result = Streaks.Calculate(new[] { WeeksAgo(2), WeeksAgo(3), WeeksAgo(4) }, Today);

        Assert.Equal(0, result.Current);
        Assert.Equal(3, result.Longest);
    }

    [Fact]
    public void AGapInThePast_SplitsTheRunsAndLongestIsTheBiggest()
    {
        var dates = new[] { WeeksAgo(0), WeeksAgo(1), WeeksAgo(5), WeeksAgo(6), WeeksAgo(7), WeeksAgo(8) };

        var result = Streaks.Calculate(dates, Today);

        Assert.Equal(2, result.Current);
        Assert.Equal(4, result.Longest);
    }

    [Fact]
    public void SeveralClassesInOneWeek_CountAsOneWeek()
    {
        var dates = new[] { WeeksAgo(0, 0), WeeksAgo(0, 1), WeeksAgo(1, 2), WeeksAgo(1, 5), WeeksAgo(1, 6) };

        var result = Streaks.Calculate(dates, Today);

        Assert.Equal(2, result.Current);
        Assert.Equal(2, result.Longest);
    }

    [Fact]
    public void SundayAndTheNextMonday_AreConsecutiveWeeks()
    {
        var sunday = new DateTime(2026, 11, 1);
        var monday = new DateTime(2026, 11, 2);

        var result = Streaks.Calculate(new[] { sunday, monday }, Today);

        Assert.Equal(2, result.Current);
    }

    [Fact]
    public void DatesAfterThisWeek_AreIgnored()
    {
        var result = Streaks.Calculate(new[] { WeeksAgo(-1), WeeksAgo(-2) }, Today);

        Assert.Equal(0, result.Current);
        Assert.Equal(0, result.Longest);
    }
}
