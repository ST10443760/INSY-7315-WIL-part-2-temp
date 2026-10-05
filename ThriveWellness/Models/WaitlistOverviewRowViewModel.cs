namespace ThriveWellness.Models
{
    // View model for one row of the admin's cross-session waitlist overview
    // - see AdminDashboardService.GetWaitlistOverviewAsync.
    public class WaitlistOverviewRowViewModel
    {
        public int WaitlistId { get; set; }
        public int SessionId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
        public int Position { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; }
    }
}
