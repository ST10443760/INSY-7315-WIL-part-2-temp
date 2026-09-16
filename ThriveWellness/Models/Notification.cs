namespace ThriveWellness.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public int BookingId { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }
}
