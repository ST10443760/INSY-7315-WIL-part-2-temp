namespace ThriveWellness.Models
{
    public enum CancelPreviewStatus
    {
        Ready,
        NotFound,
        NotOwner,

        // Already cancelled, or the class has started.
        NotAllowed
    }

    // The client's "cancel this booking?" page: which class, and what will
    // happen to their payment if they go ahead.
    public class CancelBookingPreview
    {
        public CancelPreviewStatus Status { get; set; }
        public string? Message { get; set; }
        public int BookingId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
        public RefundResult Refund { get; set; } = RefundResult.Nothing;
    }
}
