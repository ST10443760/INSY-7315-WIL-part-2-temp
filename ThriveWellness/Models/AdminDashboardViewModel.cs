namespace ThriveWellness.Models
{
    public class AdminDashboardViewModel
    {
        public int PendingPaymentsCount { get; set; }
        public int NewClientCount { get; set; }
        public int ReturningClientCount { get; set; }
        public int WaitlistedClientCount { get; set; }
        public IReadOnlyList<SessionOverviewViewModel> UpcomingSessions { get; set; } = new List<SessionOverviewViewModel>();
        public IReadOnlyList<RecentBookingViewModel> RecentBookings { get; set; } = new List<RecentBookingViewModel>();
    }
}
