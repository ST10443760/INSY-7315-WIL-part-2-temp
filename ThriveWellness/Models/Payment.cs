namespace ThriveWellness.Models
{
    // Entity: one row per booking's payment, created Pending alongside the
    // booking and flipped to Confirmed by PaymentService once an admin
    // verifies an EFT or cash payment came in.
    public class Payment
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
    }
}
