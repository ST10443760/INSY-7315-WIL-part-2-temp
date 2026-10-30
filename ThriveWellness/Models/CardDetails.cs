namespace ThriveWellness.Models
{
    // The card a client typed into the checkout form, handed straight to
    // IPaymentGateway.ChargeAsync and then dropped. Never an EF entity, never
    // stored, never logged: only the brand and last four digits that come
    // back on the GatewayChargeResult are kept (see PaymentTransaction).
    public class CardDetails
    {
        public string CardholderName { get; set; } = string.Empty;

        // Digits only - the controller strips the spaces the form adds.
        public string Number { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }

        // Four-digit year (the form's YY is expanded before it gets here).
        public int ExpiryYear { get; set; }
        public string Cvc { get; set; } = string.Empty;

        // Overridden so the card can never end up in a log line or an
        // exception message by accident, for example through string
        // interpolation or a debugger display.
        public override string ToString() => "[card details hidden]";
    }
}
