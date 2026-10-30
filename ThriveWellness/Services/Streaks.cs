namespace ThriveWellness.Services
{
    // A client's weekly streak: how many weeks in a row (Monday to Sunday,
    // the same weeks as the schedule - ScheduleWeek.StartOfWeek) have at
    // least one attended class. Pure: takes the attended class dates and
    // today, touches no database or clock, so every rule is unit tested.
    public static class Streaks
    {
        public record Result(int Current, int Longest, bool AttendedThisWeek);

        // Current counts back from this week. A week that's still in
        // progress gets grace: if there's no class this week yet, the streak
        // carries on from last week, so it only resets once a whole week
        // has passed with no class. Longest is the longest run ever.
        public static Result Calculate(IEnumerable<DateTime> attendedDates, DateTime today)
        {
            var thisWeek = ScheduleWeek.StartOfWeek(today);
            var weeks = attendedDates
                .Select(ScheduleWeek.StartOfWeek)
                .Where(week => week <= thisWeek)
                .ToHashSet();

            var attendedThisWeek = weeks.Contains(thisWeek);
            var current = 0;
            for (var week = attendedThisWeek ? thisWeek : thisWeek.AddDays(-7); weeks.Contains(week); week = week.AddDays(-7))
            {
                current++;
            }

            var longest = 0;
            var run = 0;
            DateTime? previous = null;
            foreach (var week in weeks.OrderBy(w => w))
            {
                run = previous.HasValue && week == previous.Value.AddDays(7) ? run + 1 : 1;
                longest = Math.Max(longest, run);
                previous = week;
            }

            return new Result(current, longest, attendedThisWeek);
        }
    }
}
