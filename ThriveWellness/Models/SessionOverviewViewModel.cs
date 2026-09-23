namespace ThriveWellness.Models
{
    public class SessionOverviewViewModel
    {
        public int SessionId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int BookedCount { get; set; }
        public int Capacity { get; set; }
        public bool IsOpen { get; set; }

        // Full if an admin closed it (FR-17) or bookings have reached capacity.
        public bool IsFull => !IsOpen || BookedCount >= Capacity;
    }
}
