namespace ThriveWellness.Models
{
    // Entity: one row per card charge attempt against a Payment, succeeded
    // or declined, so a payment's full history (a decline, then a retry with
    // another card) is kept. Deliberately has nowhere to put a full card
    // number, expiry date or CVC - only the brand and last four digits the
    // gateway hands back (PaymentTransactionSecurityTests checks this).
    public class PaymentTransaction
    {
        public int PaymentTransactionId { get; set; }
        public int PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ZAR";

        // See PaymentTransactionStatus.
        public string Status { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;

        // Empty for a card the gateway rejected before charging it.
        public string GatewayReference { get; set; } = string.Empty;
        public string? FailureCode { get; set; }
        public string? FailureMessage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class PaymentTransactionStatus
    {
        public const string Succeeded = "Succeeded";
        public const string Declined = "Declined";

        // A charge that succeeded and was later refunded because the
        // booking was cancelled.
        public const string Refunded = "Refunded";
    }
}
