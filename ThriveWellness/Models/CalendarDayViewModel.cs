namespace ThriveWellness.Models
{
    public class CalendarDayViewModel
    {
        public DateTime Date { get; set; }
        public IReadOnlyList<SessionOverviewViewModel> Sessions { get; set; } = new List<SessionOverviewViewModel>();
    }
}
