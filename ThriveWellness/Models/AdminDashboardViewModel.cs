namespace ThriveWellness.Models
{
    // View model for the main admin dashboard page - the aggregate result of
    // AdminDashboardService.GetDashboardAsync.
    public class AdminDashboardViewModel
    {
        public int PendingPaymentsCount { get; set; }
        public int NewClientCount { get; set; }
        public int ReturningClientCount { get; set; }
        public int WaitlistedClientCount { get; set; }
        public IReadOnlyList<SessionOverviewViewModel> UpcomingSessions { get; set; } = new List<SessionOverviewViewModel>();

        // A short preview for the dashboard's own Pending Payments table
        // (desktop-admin-dashboard.png) - the full list lives on the
        // Payments page itself, linked via "View all pending payments".
        public IReadOnlyList<PaymentOverviewViewModel> PendingPaymentsPreview { get; set; } = new List<PaymentOverviewViewModel>();

        // Last 10 bookings, most recent first - FR-13.
        public IReadOnlyList<RecentBookingViewModel> RecentBookings { get; set; } = new List<RecentBookingViewModel>();

        // Sessions starting in the next 7 days don't map 1:1 to "bookings" -
        // this sums each of those sessions' own active booking count, so
        // it's a real count of upcoming bookings, not sessions.
        public int UpcomingBookingsCount => UpcomingSessions.Sum(s => s.BookedCount);
    }
}
