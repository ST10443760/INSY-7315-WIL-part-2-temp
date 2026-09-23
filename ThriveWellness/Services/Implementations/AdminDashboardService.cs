using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private const int UpcomingWindowDays = 7;
        private const int RecentBookingsCount = 10;

        private readonly ApplicationDbContext _context;

        public AdminDashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

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
                RecentBookings = recentBookings
            };
        }

        public async Task<IReadOnlyList<CalendarDayViewModel>> GetCalendarAsync()
        {
            var sessions = await SessionOverviews()
                .OrderBy(s => s.Date).ThenBy(s => s.Time)
                .ToListAsync();

            return sessions
                .GroupBy(s => s.Date.Date)
                .Select(g => new CalendarDayViewModel { Date = g.Key, Sessions = g.ToList() })
                .ToList();
        }

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
                    SessionId = session.SessionId,
                    SessionType = session.SessionType,
                    SessionDate = session.Date,
                    SessionTime = session.Time,
                    LocationName = location.Name,
                    Position = entry.Position,
                    ClientName = client.FullName,
                    ClientEmail = client.Email,
                    DateAdded = entry.DateAdded
                }).ToListAsync();
        }

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
