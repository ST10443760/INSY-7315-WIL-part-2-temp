namespace ThriveWellness.Models
{
    // View model for one row of the public schedule - see
    // SessionRepository.GetScheduleAsync. The IsFull/AvailableSpots/
    // IsLowAvailability properties exist so the Razor view never has to
    // repeat this arithmetic itself.
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

        // Raw number, kept for capacity checks even though the schedule only
        // ever shows a status ("Spots available" / "X spots remaining"), not
        // this number itself once IsLowAvailability is true.
        public int AvailableSpots => Capacity - BookedCount;

        // 1-3 spots left is called out as low availability; 4+ is just
        // "Spots available" with no number attached.
        public bool IsLowAvailability => !IsFull && AvailableSpots <= 3;
    }
}
