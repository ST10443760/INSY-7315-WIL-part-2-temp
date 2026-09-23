namespace ThriveWellness.Models
{
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
