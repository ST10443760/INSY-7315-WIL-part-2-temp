namespace ThriveWellness.Models
{
    // Entity: one row per client waiting for a full session (FR-11).
    public class Waitlist
    {
        public int WaitlistId { get; set; }
        public int ClientId { get; set; }
        public int SessionId { get; set; }

        // 1-based queue order for this session - see WaitlistService for how
        // it's kept gap-free as entries join, leave or get promoted.
        public int Position { get; set; }
        public DateTime DateAdded { get; set; }
    }
}
