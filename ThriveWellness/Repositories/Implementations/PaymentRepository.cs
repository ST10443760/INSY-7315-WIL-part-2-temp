using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Repositories.Implementations
{
    // Repository pattern: thin EF Core wrapper around the Payments table -
    // no business rules here, just queries and saves.
    public class PaymentRepository : IPaymentRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Payment payment)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
        }

        public async Task<Payment?> GetByIdAsync(int id)
        {
            return await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == id);
        }

        public async Task<Payment?> GetByBookingIdAsync(int bookingId)
        {
            return await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        }

        // Backs the admin payments list: every payment (optionally filtered
        // to one status) joined up with its booking, client, session and
        // location so the screen can show all of that in one row, newest
        // session first.
        public async Task<IEnumerable<PaymentOverviewViewModel>> GetAllPaymentsAsync(string? status)
        {
            var query =
                from payment in _context.Payments
                join booking in _context.Bookings on payment.BookingId equals booking.BookingId
                join client in _context.Clients on booking.ClientId equals client.ClientId
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                join location in _context.Locations on session.LocationId equals location.LocationId
                select new { payment, booking, client, session, location };

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.payment.Status == status);
            }

            var ordered = query.OrderByDescending(x => x.session.Date).ThenByDescending(x => x.session.Time);

            return await ordered.Select(x => new PaymentOverviewViewModel
            {
                PaymentId = x.payment.PaymentId,
                BookingId = x.booking.BookingId,
                ClientName = x.client.FullName,
                SessionType = x.session.SessionType,
                SessionDate = x.session.Date,
                SessionTime = x.session.Time,
                LocationName = x.location.Name,
                Amount = x.payment.Amount,
                Method = x.payment.Method,
                PaymentType = x.payment.PaymentType,
                Status = x.payment.Status
            }).ToListAsync();
        }

        public async Task UpdateStatusAsync(int paymentId, string status)
        {
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
            if (payment != null)
            {
                payment.Status = status;
                await _context.SaveChangesAsync();
            }
        }
    }
}
