namespace ThriveWellness.Models
{
    // View model for the signed-in client's dashboard: their bookings,
    // split into upcoming and past.
    public class ClientDashboardViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public IReadOnlyList<ClientBookingViewModel> Upcoming { get; set; } = Array.Empty<ClientBookingViewModel>();
        public IReadOnlyList<ClientBookingViewModel> Past { get; set; } = Array.Empty<ClientBookingViewModel>();
    }

    // One of the client's own bookings, flattened with its session, studio
    // and payment for display.
    public class ClientBookingViewModel
    {
        public int BookingId { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? PaymentStatus { get; set; }
        public decimal? Amount { get; set; }
        public string CancellationToken { get; set; } = string.Empty;
    }
}
