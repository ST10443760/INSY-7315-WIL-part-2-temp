using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private const int UpcomingWindowDays = 7;
        private const int PendingPaymentsPreviewCount = 5;

        private readonly ApplicationDbContext _context;
        private readonly IPaymentRepository _paymentRepository;

        public AdminDashboardService(ApplicationDbContext context, IPaymentRepository paymentRepository)
        {
            _context = context;
            _paymentRepository = paymentRepository;
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

            var pendingPayments = (await _paymentRepository.GetPendingPaymentsAsync())
                .Take(PendingPaymentsPreviewCount)
                .ToList();

            return new AdminDashboardViewModel
            {
                PendingPaymentsCount = await _context.Payments.CountAsync(p => p.Status == "Pending"),
                NewClientCount = await _context.Clients.CountAsync(c => c.IsNew),
                ReturningClientCount = await _context.Clients.CountAsync(c => !c.IsNew),
                WaitlistedClientCount = await _context.Waitlists.Select(w => w.ClientId).Distinct().CountAsync(),
                UpcomingSessions = upcomingSessions,
                PendingPaymentsPreview = pendingPayments
            };
        }

        public async Task<IReadOnlyList<SessionOverviewViewModel>> GetSessionOverviewsAsync()
        {
            return await SessionOverviews()
                .OrderBy(s => s.Date).ThenBy(s => s.Time)
                .ToListAsync();
        }

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
