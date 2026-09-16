namespace ThriveWellness.Models
{
    public class Session
    {
        public int SessionId { get; set; }
        public int LocationId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public int Capacity { get; set; }
        public bool IsOpen { get; set; }
    }
}
