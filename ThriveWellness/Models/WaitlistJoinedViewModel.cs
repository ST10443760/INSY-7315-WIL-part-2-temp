namespace ThriveWellness.Models
{
    // View model for the confirmation page shown right after
    // WaitlistService.JoinWaitlistAsync succeeds - tells the client which
    // class they're queued for and where they landed in line.
    public class WaitlistJoinedViewModel
    {
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
        public int Position { get; set; }
    }
}
