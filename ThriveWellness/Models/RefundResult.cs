namespace ThriveWellness.Models
{
    // What happens to a booking's payment when the booking is cancelled.
    public enum RefundOutcome
    {
        // Nothing was paid, so there's nothing to give back.
        None,

        // Paid by card: refunded through the payment gateway.
        CardRefunded,

        // Paid by EFT or cash (or a card refund that didn't go through): the
        // studio has to pay it back, so it shows as Refund due for the admin.
        RefundDue
    }

    public class RefundResult
    {
        public RefundOutcome Outcome { get; set; }
        public decimal Amount { get; set; }

        public static RefundResult Nothing => new() { Outcome = RefundOutcome.None };
    }
}
