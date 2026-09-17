namespace ThriveWellness.Models
{
    public class BookingConfirmationViewModel
    {
        public int BookingId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CancellationToken { get; set; } = string.Empty;
    }
}
