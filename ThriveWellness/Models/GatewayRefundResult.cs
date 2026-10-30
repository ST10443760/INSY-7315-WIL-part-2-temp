namespace ThriveWellness.Models
{
    // A payment gateway's answer to a refund request.
    public class GatewayRefundResult
    {
        public bool Success { get; set; }

        // The provider's id for the refund, for example "TW-RF7K2M9QX4".
        public string RefundReference { get; set; } = string.Empty;
        public string? FailureMessage { get; set; }
    }
}
