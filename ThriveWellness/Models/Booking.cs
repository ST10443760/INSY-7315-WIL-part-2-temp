namespace ThriveWellness.Models
{
    public class Booking
    {
        public int BookingId { get; set; }
        public int ClientId { get; set; }
        public int SessionId { get; set; }
        public DateTime BookingDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CancellationToken { get; set; } = string.Empty;
    }
}
