namespace ThriveWellness.Models
{
    // Entity: one row per client who's ever started a booking - matched by
    // Email in BookingService.CheckClientStatusAsync, which is how a
    // returning client is recognised without logging in.
    public class Client
    {
        public int ClientId { get; set; }
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
