namespace ThriveWellness.Models
{
    // View model for the checkout page: the order summary on the left and
    // the card form on the right.
    public class CheckoutViewModel
    {
        public CheckoutSummary Summary { get; set; } = new();

        // Only the cardholder name survives a failed attempt; see
        // CardPaymentForm.
        public string CardholderName { get; set; } = string.Empty;

        // The friendly decline or validation message after a failed attempt.
        public string? ErrorMessage { get; set; }

        // Set when this booking can't be paid (cancelled, class already
        // started, not a card booking) - the page then shows this instead
        // of the form.
        public string? UnavailableMessage { get; set; }
        public bool IsTestMode { get; set; }
    }
}
