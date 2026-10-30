namespace ThriveWellness.Services
{
    // The week the public schedule is showing, worked out from the
    // ?week=YYYY-MM-DD query value. Pure date arithmetic with no database
    // or clock access of its own (today is passed in), so every rule here
    // is unit tested directly.
    //
    // Rules: a week always starts on a Monday - any other date snaps back
    // to its Monday. Browsing is clamped between the current week and the
    // week of the last scheduled session, so Previous never shows weeks
    // that have already passed and Next never runs on into empty weeks.
    public record ScheduleWeek(DateTime Start, DateTime? PreviousStart, DateTime? NextStart, bool IsThisWeek)
    {
        // The Sunday that ends the week, for the "2 - 8 November" label.
        public DateTime End => Start.AddDays(6);

        public static ScheduleWeek Resolve(DateTime? requested, DateTime today, DateTime? lastSessionDate)
        {
            var thisWeek = StartOfWeek(today);
            var lastWeek = lastSessionDate.HasValue ? StartOfWeek(lastSessionDate.Value) : thisWeek;
            if (lastWeek < thisWeek)
            {
                lastWeek = thisWeek;
            }

            var start = StartOfWeek(requested ?? today);
            if (start < thisWeek)
            {
                start = thisWeek;
            }
            else if (start > lastWeek)
            {
                start = lastWeek;
            }

            return new ScheduleWeek(
                start,
                start > thisWeek ? start.AddDays(-7) : null,
                start < lastWeek ? start.AddDays(7) : null,
                start == thisWeek);
        }

        // DayOfWeek counts from Sunday = 0, so shifting by 6 (mod 7) gives
        // days since Monday: Monday 0, Tuesday 1, ... Sunday 6.
        public static DateTime StartOfWeek(DateTime date)
        {
            var day = date.Date;
            var daysSinceMonday = ((int)day.DayOfWeek + 6) % 7;
            return day.AddDays(-daysSinceMonday);
        }

        // "2 - 8 November 2026", or "26 October - 1 November 2026" when the
        // week spans two months (and the year too if it spans two years).
        public string RangeLabel()
        {
            if (Start.Year != End.Year)
            {
                return $"{Start:d MMMM yyyy} - {End:d MMMM yyyy}";
            }
            if (Start.Month != End.Month)
            {
                return $"{Start:d MMMM} - {End:d MMMM yyyy}";
            }
            return $"{Start.Day} - {End:d MMMM yyyy}";
        }
    }
}
