using ThriveWellness.Services;

namespace ThriveWellness.Models
{
    // View model for the signed-in client's dashboard: summary tiles, their
    // upcoming and past bookings, their payments and receipts, and the
    // waitlists they're on. Built by ClientDashboardService.
    public class ClientDashboardViewModel
    {
        public string FirstName { get; set; } = string.Empty;

        // Booked classes still to come, not counting cancelled ones.
        public int UpcomingCount { get; set; }

        // Classes that have happened, were paid for and weren't cancelled.
        public int AttendedCount { get; set; }

        // Everything paid and not refunded.
        public decimal TotalSpent { get; set; }

        public IReadOnlyList<ClientBookingViewModel> Upcoming { get; set; } = Array.Empty<ClientBookingViewModel>();
        public IReadOnlyList<ClientBookingViewModel> Past { get; set; } = Array.Empty<ClientBookingViewModel>();
        public IReadOnlyList<ClientPaymentViewModel> Payments { get; set; } = Array.Empty<ClientPaymentViewModel>();
        public IReadOnlyList<ClientWaitlistViewModel> Waitlist { get; set; } = Array.Empty<ClientWaitlistViewModel>();

        // Streak and badges, filled in by the controller from RewardsService.
        public RewardsViewModel Rewards { get; set; } = new();
    }

    // One of the client's own bookings, flattened with its session, studio
    // and payment for display.
    public class ClientBookingViewModel
    {
        public int BookingId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? PaymentId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentStatus { get; set; }
        public decimal? Amount { get; set; }
        public string CancellationToken { get; set; } = string.Empty;
        public bool NoShow { get; set; }

        public DateTime StartsAt => SessionDate.Date + SessionTime;
        public bool IsCancelled => Status == "Cancelled";
        public bool IsPaid => Attendance.IsPaid(Status, PaymentStatus);

        // The chip shown on the dashboard: Cancelled, No-show, Paid or
        // Awaiting payment.
        public string StatusLabel => IsCancelled ? "Cancelled" : NoShow ? "No-show" : IsPaid ? "Paid" : "Awaiting payment";

        public bool IsAttended(DateTime now) => Attendance.IsAttended(Status, NoShow, PaymentStatus, StartsAt, now);

        public bool CanPayByCard =>
            !IsCancelled && PaymentStatus == PaymentStatuses.Pending && PaymentMethods.IsCard(PaymentMethod);
    }

    // A payment the client has made (or had refunded), for the Payments and
    // receipts list. Unpaid bookings aren't listed here - they show under
    // Upcoming with a Pay now button instead.
    public class ClientPaymentViewModel
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        // Card payments only: when the charge went through and with which
        // card. A receipt exists exactly when PaidAt is set.
        public DateTime? PaidAt { get; set; }
        public string? Brand { get; set; }
        public string? Last4 { get; set; }

        public bool HasReceipt => PaidAt.HasValue;
    }

    // A session the client is waiting for a place in.
    public class ClientWaitlistViewModel
    {
        public int SessionId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
        public int Position { get; set; }

        public DateTime StartsAt => SessionDate.Date + SessionTime;
    }
}
