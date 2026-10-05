namespace ThriveWellness.Models
{
    // Return value of BookingService.CancelBookingAsync and CancelByAdminAsync.
    public class CancelBookingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
