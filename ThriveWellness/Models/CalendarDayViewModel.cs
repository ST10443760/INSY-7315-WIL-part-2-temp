namespace ThriveWellness.Models
{
    // One day's worth of sessions for the admin calendar grid - see
    // AdminCalendarViewModel and AdminDashboardService.GetCalendarAsync.
    public class CalendarDayViewModel
    {
        public DateTime Date { get; set; }
        public IReadOnlyList<SessionOverviewViewModel> Sessions { get; set; } = new List<SessionOverviewViewModel>();
    }
}
