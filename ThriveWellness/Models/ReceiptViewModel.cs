namespace ThriveWellness.Models
{
    // The receipt for one card payment: what was paid, when, with which card
    // (brand and last four only) and for which class.
    public class ReceiptViewModel
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ZAR";
        public DateTime PaidAt { get; set; }
        public string GatewayReference { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
    }

    public enum ReceiptStatus
    {
        Ready,
        NotFound,
        NotOwner,
        NotPaid
    }

    // Return value of CheckoutService.GetReceiptAsync.
    public class ReceiptResult
    {
        public ReceiptStatus Status { get; set; }
        public ReceiptViewModel? Receipt { get; set; }
    }
}
