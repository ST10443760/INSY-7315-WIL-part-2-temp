namespace ThriveWellness.Models
{
    public class Waitlist
    {
        public int WaitlistId { get; set; }
        public int ClientId { get; set; }
        public int SessionId { get; set; }
        public int Position { get; set; }
        public DateTime DateAdded { get; set; }
    }
}
