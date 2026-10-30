namespace ThriveWellness.Models
{
    // The values Payment.Status can hold. Pending until it's paid, then
    // Confirmed - by the admin for EFT and cash, or automatically once a
    // card charge succeeds at checkout. When a paid booking is cancelled, a
    // card payment is refunded through the gateway (Refunded); an EFT or
    // cash payment becomes Refund due until the studio pays it back.
    public static class PaymentStatuses
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string Refunded = "Refunded";
        public const string RefundDue = "Refund due";
    }
}
