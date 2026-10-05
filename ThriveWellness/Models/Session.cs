namespace ThriveWellness.Models
{
    // Entity: one row per class instance - each date/time is its own row,
    // not an occurrence of a recurring schedule.
    public class Session
    {
        public int SessionId { get; set; }
        public int LocationId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public int Capacity { get; set; }

        // The admin's manual open/closed switch (MarkAsFullAsync /
        // MarkAsOpenAsync) - separate from whether the session has actually
        // reached Capacity, which is computed from live booking counts
        // rather than stored here.
        public bool IsOpen { get; set; }
    }
}
