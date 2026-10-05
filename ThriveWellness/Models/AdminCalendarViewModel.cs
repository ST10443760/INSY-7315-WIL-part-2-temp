namespace ThriveWellness.Models
{
    // View model for the admin calendar page - wraps
    // AdminDashboardService.GetCalendarAsync's per-day results with the two
    // extra values the Razor view needs to actually lay out a grid (which
    // month, and how many blank cells come before day 1).
    public class AdminCalendarViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public IReadOnlyList<CalendarDayViewModel> Days { get; set; } = new List<CalendarDayViewModel>();

        public DateTime MonthStart => new(Year, Month, 1);

        // Leading blank cells so day 1 lands in its real Sun-Sat column.
        public int LeadingBlankDays => (int)MonthStart.DayOfWeek;
    }
}
