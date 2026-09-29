namespace ThriveWellness.Models
{
    public class SessionListItemViewModel
    {
        public int SessionId { get; set; }
        public string LocationName { get; set; } = string.Empty;

        // Short area name (e.g. "Sunninghill", "Kyalami") - used for the
        // schedule's location filter tabs/pills.
        public string LocationAddress { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public int Capacity { get; set; }
        public bool IsOpen { get; set; }

        // Active (non-cancelled) bookings against this session.
        public int BookedCount { get; set; }

        // Full if an admin closed it (FR-17) or bookings have reached capacity.
        public bool IsFull => !IsOpen || BookedCount >= Capacity;
    }
}
