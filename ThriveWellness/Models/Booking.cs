namespace ThriveWellness.Models
{
    // Entity: one row per booking attempt, confirmed or not. Lives through
    // BookingService's whole lifecycle - created in CreateBookingAsync,
    // flipped to Confirmed by PaymentService, and flipped to Cancelled by
    // either cancellation path.
    public class Booking
    {
        public int BookingId { get; set; }
        public int ClientId { get; set; }
        public int SessionId { get; set; }
        public DateTime BookingDate { get; set; }

        // A plain string ("Awaiting Payment"/"Confirmed"/"Cancelled") rather
        // than an enum - checked by string comparison throughout the
        // services and repositories, so changing these values means
        // updating every one of those checks, not just this property.
        public string Status { get; set; } = string.Empty;

        // The FR-18 cancellation link's token - see
        // CancellationTokenGenerator for how it's generated and what
        // "single use" means for it.
        public string CancellationToken { get; set; } = string.Empty;
    }
}
