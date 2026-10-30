using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: everything the client dashboard shows, for one client.
    // GetDashboardAsync runs the queries; Build (static, no database) does
    // the sorting and adding up, so the rules - what counts as upcoming,
    // attended, spent - are tested without a database.
    public class ClientDashboardService : IClientDashboardService
    {
        private readonly IBookingService _bookingService;
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;

        public ClientDashboardService(IBookingService bookingService, ApplicationDbContext context, TimeProvider timeProvider)
        {
            _bookingService = bookingService;
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<ClientDashboardViewModel> GetDashboardAsync(int clientId, string firstName)
        {
            var bookings = await _bookingService.GetClientBookingsAsync(clientId);

            // Each payment with its card charge, if it had one (the latest
            // charge that went through - a declined attempt isn't a receipt).
            var payments = await (
                from payment in _context.Payments
                join booking in _context.Bookings on payment.BookingId equals booking.BookingId
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                where booking.ClientId == clientId
                let charge = _context.PaymentTransactions
                    .Where(t => t.PaymentId == payment.PaymentId && t.Status != PaymentTransactionStatus.Declined)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefault()
                select new ClientPaymentViewModel
                {
                    PaymentId = payment.PaymentId,
                    BookingId = booking.BookingId,
                    SessionType = session.SessionType,
                    SessionDate = session.Date,
                    Amount = payment.Amount,
                    Method = payment.Method,
                    Status = payment.Status,
                    PaidAt = charge != null ? charge.CreatedAt : null,
                    Brand = charge != null ? charge.Brand : null,
                    Last4 = charge != null ? charge.Last4 : null
                }).ToListAsync();

            var today = _timeProvider.GetLocalNow().Date;
            var waitlist = await (
                from entry in _context.Waitlists
                join session in _context.Sessions on entry.SessionId equals session.SessionId
                join location in _context.Locations on session.LocationId equals location.LocationId
                where entry.ClientId == clientId && session.Date >= today
                select new ClientWaitlistViewModel
                {
                    SessionId = session.SessionId,
                    SessionType = session.SessionType,
                    SessionDate = session.Date,
                    SessionTime = session.Time,
                    LocationName = location.Name,
                    LocationAddress = location.Address,
                    Position = entry.Position
                }).ToListAsync();

            return Build(firstName, bookings, payments, waitlist, _timeProvider.GetLocalNow().DateTime);
        }

        // The dashboard's rules, all in one place:
        // - upcoming means the class hasn't started yet (date and time), so a
        //   class later today is still upcoming; cancelled ones stay listed
        //   with a Cancelled chip but don't count in the tile;
        // - attended is the shared Attendance rule (started, not cancelled,
        //   paid, not a no-show), the same one rewards use;
        // - total spent is every confirmed payment - pending ones aren't paid
        //   yet, and refunded ones are given back;
        // - the payments list shows what's been paid or refunded, newest
        //   class first; unpaid bookings show under Upcoming instead.
        public static ClientDashboardViewModel Build(
            string firstName,
            IEnumerable<ClientBookingViewModel> bookings,
            IEnumerable<ClientPaymentViewModel> payments,
            IEnumerable<ClientWaitlistViewModel> waitlist,
            DateTime now)
        {
            var bookingList = bookings.ToList();
            var paymentList = payments.ToList();

            var upcoming = bookingList
                .Where(b => b.StartsAt > now)
                .OrderBy(b => b.StartsAt)
                .ToList();

            var past = bookingList
                .Where(b => b.StartsAt <= now)
                .OrderByDescending(b => b.StartsAt)
                .ToList();

            return new ClientDashboardViewModel
            {
                FirstName = firstName,
                UpcomingCount = upcoming.Count(b => !b.IsCancelled),
                AttendedCount = past.Count(b => b.IsAttended(now)),
                TotalSpent = paymentList.Where(p => p.Status == PaymentStatuses.Confirmed).Sum(p => p.Amount),
                Upcoming = upcoming,
                Past = past,
                Payments = paymentList
                    .Where(p => p.Status != PaymentStatuses.Pending)
                    .OrderByDescending(p => p.SessionDate)
                    .ThenByDescending(p => p.PaymentId)
                    .ToList(),
                Waitlist = waitlist
                    .Where(w => w.StartsAt > now)
                    .OrderBy(w => w.StartsAt)
                    .ToList()
            };
        }
    }
}
