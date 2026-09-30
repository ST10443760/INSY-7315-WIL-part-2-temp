namespace ThriveWellness.Models
{
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
