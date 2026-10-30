namespace ThriveWellness.Models
{
    // What the app asks a payment gateway to charge: an amount in rands, the
    // currency, the booking it's for and the card. Shaped like a real
    // provider's charge call so PayFast or Stripe could sit behind
    // IPaymentGateway without the callers changing.
    public class GatewayChargeRequest
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ZAR";

        // Shown on the provider's side so a charge can be traced back to the
        // booking, for example "Booking 42".
        public string BookingReference { get; set; } = string.Empty;
        public CardDetails Card { get; set; } = new();
    }
}
