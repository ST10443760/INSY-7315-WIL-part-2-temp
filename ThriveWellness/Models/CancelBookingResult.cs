namespace ThriveWellness.Models
{
    // Return value of BookingService.CancelBookingAsync, CancelByAdminAsync
    // and CancelByClientAsync.
    public class CancelBookingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        // Set when a signed-in client tried to cancel someone else's
        // booking - the controller answers 403 rather than a message.
        public bool NotOwner { get; set; }

        // What happened to the payment, once the booking is cancelled.
        public RefundResult Refund { get; set; } = RefundResult.Nothing;
    }
}
