namespace ThriveWellness.Models
{
    // A payment gateway's answer to a charge. Carries only what's safe to
    // keep: the brand and last four digits, never the full card.
    public class GatewayChargeResult
    {
        public bool Success { get; set; }

        // The provider's id for this charge, for example "TW-7K2M9QX4PL".
        // Empty when the card was rejected before a charge was attempted.
        public string GatewayReference { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;

        // Machine-readable reason, for example "card_declined", and the
        // friendly sentence shown to the client. Both null on success.
        public string? FailureCode { get; set; }
        public string? FailureMessage { get; set; }
    }
}
