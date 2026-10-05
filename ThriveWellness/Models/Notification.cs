namespace ThriveWellness.Models
{
    // Entity: a log row for each email actually sent about a booking (Type
    // is "Welcome"/"Confirmation"/"Reminder"/etc.) - this is what
    // ScheduledNotificationService checks to avoid sending the same
    // reminder or location email twice.
    public class Notification
    {
        public int NotificationId { get; set; }
        public int BookingId { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }
}
