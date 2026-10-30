using ThriveWellness.Services;

namespace ThriveWellness.Models
{
    // One booking on the admin's attendee list for a session - see
    // AdminDashboardService.GetAttendeesAsync.
    public class SessionAttendeeViewModel
    {
        public int BookingId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = string.Empty;
        public string? PaymentStatus { get; set; }
        public string? PaymentMethod { get; set; }
        public bool NoShow { get; set; }

        public bool IsCancelled => BookingStatus == "Cancelled";
        public bool IsPaid => Attendance.IsPaid(BookingStatus, PaymentStatus);
    }
}
