namespace ThriveWellness.Models
{
    // Entity: one row per client. Clients sign in with Google; the first
    // sign-in links to an existing row by email (so earlier bookings keep
    // their history) or creates a new one - see ClientAccountService.
    public class Client
    {
        public int ClientId { get; set; }

        // Google's stable account id ("sub" claim). Null until the client
        // first signs in with Google; unique when set.
        public string? GoogleSubject { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        // True until this client's second booking attempt - set back to
        // false the moment CreateBookingAsync recognises them as already
        // existing. Drives whether ScheduledNotificationService sends them a
        // first-timer location email on the day of a class.
        public bool IsNew { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
