namespace ThriveWellness.Models
{
    public class BookingCreateResult
    {
        public bool Success { get; set; }
        public int? BookingId { get; set; }
        public string? CancellationToken { get; set; }
        public bool RequiresWaitlist { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
