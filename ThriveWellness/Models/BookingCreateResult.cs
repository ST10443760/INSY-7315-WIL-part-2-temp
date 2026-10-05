namespace ThriveWellness.Models
{
    // Return value of BookingService.CreateBookingAsync - carries either a
    // created booking's details, or (RequiresWaitlist/ErrorMessage) enough
    // information for the controller to redirect somewhere else instead of
    // showing a confirmation.
    public class BookingCreateResult
    {
        public bool Success { get; set; }
        public int? BookingId { get; set; }
        public int? ClientId { get; set; }
        public string? CancellationToken { get; set; }
        public bool RequiresWaitlist { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
