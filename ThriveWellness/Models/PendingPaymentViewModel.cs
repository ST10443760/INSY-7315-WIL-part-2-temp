namespace ThriveWellness.Models
{
    public class PendingPaymentViewModel
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
    }
}
