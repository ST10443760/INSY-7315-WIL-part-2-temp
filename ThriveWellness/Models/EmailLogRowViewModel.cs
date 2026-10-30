namespace ThriveWellness.Models
{
    // View model for one row on the admin Emails page: a Notification row
    // joined to its booking's client, so the admin can see who each email
    // went to.
    public class EmailLogRowViewModel
    {
        public int NotificationId { get; set; }
        public DateTime SentAt { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        // Notification.Status: "Sent" once SendGrid accepted the email,
        // "Failed" if it rejected it (see NotificationService).
        public string Status { get; set; } = NotificationStatus.Sent;

        public string TypeLabel => EmailTypes.ToLabel(Type);
    }

    // The Notification.Type values NotificationService writes, in the order
    // the Emails page offers them as filters, with friendlier labels. The
    // stored values stay as they are; only what the admin reads changes.
    public static class EmailTypes
    {
        public static readonly IReadOnlyList<string> All = new[]
        {
            "Confirmation", "Welcome", "Reminder", "Location", "WaitlistPromotion", "Cancellation"
        };

        public static string ToLabel(string type) => type switch
        {
            "Confirmation" => "Booking received",
            "Welcome" => "Payment confirmed",
            "Reminder" => "Reminder",
            "Location" => "Location",
            "WaitlistPromotion" => "Off the waitlist",
            "Cancellation" => "Cancellation",
            _ => type
        };
    }
}
