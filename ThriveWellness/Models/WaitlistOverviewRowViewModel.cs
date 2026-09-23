namespace ThriveWellness.Models
{
    public class WaitlistOverviewRowViewModel
    {
        public int SessionId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int Position { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; }
    }
}
