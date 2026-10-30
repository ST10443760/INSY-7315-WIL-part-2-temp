namespace ThriveWellness.Models
{
    // View model for one row of the admin payments list - see
    // PaymentRepository.GetAllPaymentsAsync.
    public class PaymentOverviewViewModel
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        // The booking's own status, so a cancelled booking's payment is
        // labelled as such.
        public string BookingStatus { get; set; } = string.Empty;

        // The card charge's gateway reference, for card payments that went
        // through; null for EFT, cash and unpaid card bookings.
        public string? GatewayReference { get; set; }
    }
}
