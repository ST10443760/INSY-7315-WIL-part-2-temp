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

        // The booking's payment status, so a cancelled booking's refund
        // (Refund due or Refunded) shows next to it. Null if it has none.
        public string? PaymentStatus { get; set; }
    }
}
