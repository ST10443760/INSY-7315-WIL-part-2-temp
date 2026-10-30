namespace ThriveWellness.Models
{
    // View model for the booking confirmation page shown right after
    // CreateBookingAsync succeeds - a flattened view of the booking, its
    // session, its location and its cancellation link all in one shape.
    public class BookingConfirmationViewModel
    {
        public int BookingId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        // Null when the booking somehow has no Payment row - the page then
        // just leaves the amount out of its next-step text.
        public decimal? AmountDue { get; set; }

        // Lets a card booking offer "Pay now" while it's unpaid, and its
        // receipt once it's paid.
        public int? PaymentId { get; set; }
        public string? PaymentStatus { get; set; }
        public string CancellationToken { get; set; } = string.Empty;
    }
}
