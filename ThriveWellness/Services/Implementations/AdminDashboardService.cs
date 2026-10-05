using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: read-only queries that power the admin dashboard and
    // its related screens - the headline counts, the next week of sessions,
    // a preview of pending payments, the most recent bookings (FR-13), the
    // full month calendar view, and the waitlist overview. Nothing here
    // mutates state; it exists purely to keep these joins and projections
    // out of the controller.
    public class AdminDashboardService : IAdminDashboardService
    {
        private const int UpcomingWindowDays = 7;
        private const int PendingPaymentsPreviewCount = 5;
        private const int RecentBookingsCount = 10;

        private readonly ApplicationDbContext _context;
        private readonly IPaymentRepository _paymentRepository;

        public AdminDashboardService(ApplicationDbContext context, IPaymentRepository paymentRepository)
        {
            _context = context;
            _paymentRepository = paymentRepository;
        }

        // Assembles everything the main dashboard page shows in one call:
        // headline counts, the next UpcomingWindowDays of sessions, a short
        // preview of pending payments, and the most recent bookings - run as
        // several independent queries rather than one giant join, since each
        // piece has a different shape and none of them depend on another.
        public async Task<AdminDashboardViewModel> GetDashboardAsync()
        {
            // Sessions are compared against the local calendar date, matching
            // how SessionService validates a new session's date.
            var today = DateTime.Today;
            var windowEnd = today.AddDays(UpcomingWindowDays);

            var upcomingSessions = await SessionOverviews()
                .Where(s => s.Date >= today && s.Date < windowEnd)
                .OrderBy(s => s.Date).ThenBy(s => s.Time)
                .ToListAsync();

            var pendingPayments = (await _paymentRepository.GetAllPaymentsAsync("Pending"))
                .Take(PendingPaymentsPreviewCount)
                .ToList();

            var recentBookings = await (
                from booking in _context.Bookings
                join client in _context.Clients on booking.ClientId equals client.ClientId
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                join location in _context.Locations on session.LocationId equals location.LocationId
                orderby booking.BookingDate descending, booking.BookingId descending
                select new RecentBookingViewModel
                {
                    BookingId = booking.BookingId,
                    BookingDate = booking.BookingDate,
                    ClientName = client.FullName,
                    SessionType = session.SessionType,
                    SessionDate = session.Date,
                    LocationName = location.Name,
                    Status = booking.Status
                }).Take(RecentBookingsCount).ToListAsync();

            return new AdminDashboardViewModel
            {
                PendingPaymentsCount = await _context.Payments.CountAsync(p => p.Status == "Pending"),
                NewClientCount = await _context.Clients.CountAsync(c => c.IsNew),
                ReturningClientCount = await _context.Clients.CountAsync(c => !c.IsNew),
                WaitlistedClientCount = await _context.Waitlists.Select(w => w.ClientId).Distinct().CountAsync(),
                UpcomingSessions = upcomingSessions,
                PendingPaymentsPreview = pendingPayments,
                RecentBookings = recentBookings
            };
        }

        // All sessions (not just the dashboard's upcoming window), for the
        // full session-management list screen.
        public async Task<IReadOnlyList<SessionOverviewViewModel>> GetSessionOverviewsAsync()
        {
            return await SessionOverviews()
                .OrderBy(s => s.Date).ThenBy(s => s.Time)
                .ToListAsync();
        }

        // Builds a full month's worth of calendar cells, one per day, even
        // for days with no sessions - so the calendar view can just iterate
        // the result and render something for every date without
        // special-casing gaps itself.
        public async Task<IReadOnlyList<CalendarDayViewModel>> GetCalendarAsync(int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var sessions = await SessionOverviews()
                .Where(s => s.Date >= monthStart && s.Date < monthEnd)
                .OrderBy(s => s.Time)
                .ToListAsync();

            var byDay = sessions
                .GroupBy(s => s.Date.Date)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<SessionOverviewViewModel>)g.ToList());

            var days = new List<CalendarDayViewModel>();
            for (var date = monthStart; date < monthEnd; date = date.AddDays(1))
            {
                days.Add(new CalendarDayViewModel
                {
                    Date = date,
                    Sessions = byDay.TryGetValue(date, out var daySessions) ? daySessions : new List<SessionOverviewViewModel>()
                });
            }

            return days;
        }

        // Every waitlist entry across every session, for the admin waitlist
        // screen - ordered by session first and then queue position, so
        // entries for the same class stay grouped together in the order
        // they'd actually be promoted.
        public async Task<IReadOnlyList<WaitlistOverviewRowViewModel>> GetWaitlistOverviewAsync()
        {
            return await (
                from entry in _context.Waitlists
                join client in _context.Clients on entry.ClientId equals client.ClientId
                join session in _context.Sessions on entry.SessionId equals session.SessionId
                join location in _context.Locations on session.LocationId equals location.LocationId
                orderby session.Date, session.Time, session.SessionId, entry.Position
                select new WaitlistOverviewRowViewModel
                {
                    WaitlistId = entry.WaitlistId,
                    SessionId = session.SessionId,
                    SessionType = session.SessionType,
                    SessionDate = session.Date,
                    SessionTime = session.Time,
                    LocationName = location.Name,
                    LocationAddress = location.Address,
                    Position = entry.Position,
                    ClientName = client.FullName,
                    ClientEmail = client.Email,
                    DateAdded = entry.DateAdded
                }).ToListAsync();
        }

        // Shared projection used by GetDashboardAsync, GetSessionOverviewsAsync
        // and GetCalendarAsync, so the booked-count calculation below only
        // has to agree with BookingService/WaitlistService's own capacity
        // checks in one place.
        // Cancelled bookings don't count against capacity, matching the
        // capacity checks in BookingService and WaitlistService.
        private IQueryable<SessionOverviewViewModel> SessionOverviews()
        {
            return
                from session in _context.Sessions
                join location in _context.Locations on session.LocationId equals location.LocationId
                select new SessionOverviewViewModel
                {
                    SessionId = session.SessionId,
                    SessionType = session.SessionType,
                    Date = session.Date,
                    Time = session.Time,
                    LocationName = location.Name,
                    Capacity = session.Capacity,
                    IsOpen = session.IsOpen,
                    BookedCount = _context.Bookings.Count(b => b.SessionId == session.SessionId && b.Status != "Cancelled")
                };
        }
    }
}
