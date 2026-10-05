namespace ThriveWellness.Models
{
    // View model for one row of the dashboard's "recent bookings" table
    // (FR-13) - see AdminDashboardService.GetDashboardAsync.
    public class RecentBookingViewModel
    {
        public int BookingId { get; set; }
        public DateTime BookingDate { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
