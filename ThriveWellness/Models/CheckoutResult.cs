namespace ThriveWellness.Models
{
    // What CheckoutService decided about a checkout request. The controller
    // maps each one to a response: NotFound to 404, NotOwner to 403,
    // AlreadyPaid and Succeeded to the receipt, the rest back to the
    // checkout page.
    public enum CheckoutStatus
    {
        Ready,
        NotFound,
        NotOwner,
        NotPayable,
        AlreadyPaid,
        InProgress,
        Declined,
        Succeeded
    }

    // Return value of CheckoutService.GetCheckoutAsync and PayAsync.
    public class CheckoutResult
    {
        public CheckoutStatus Status { get; set; }

        // The booking being paid for - set whenever the client may see it
        // (everything except NotFound and NotOwner).
        public CheckoutSummary? Summary { get; set; }

        // Set for AlreadyPaid and Succeeded, for the receipt link.
        public int? PaymentId { get; set; }

        // A sentence for the client: why it can't be paid, or why the card
        // was declined.
        public string? Message { get; set; }
    }

    // The order summary on the checkout page.
    public class CheckoutSummary
    {
        public int BookingId { get; set; }
        public int PaymentId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ZAR";
    }
}
