namespace ThriveWellness.Models
{
    // View model for one row of a single session's waitlist (as shown from
    // that session's own admin page, rather than the cross-session overview
    // - compare WaitlistOverviewRowViewModel).
    public class WaitlistEntryViewModel
    {
        public int Position { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; }
    }
}
