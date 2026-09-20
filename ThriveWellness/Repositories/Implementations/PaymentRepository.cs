using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Repositories.Implementations
{
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

        public async Task<IEnumerable<PendingPaymentViewModel>> GetPendingPaymentsAsync()
        {
            var query =
                from payment in _context.Payments
                where payment.Status == "Pending"
                join booking in _context.Bookings on payment.BookingId equals booking.BookingId
                join client in _context.Clients on booking.ClientId equals client.ClientId
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                join location in _context.Locations on session.LocationId equals location.LocationId
                select new PendingPaymentViewModel
                {
                    PaymentId = payment.PaymentId,
                    BookingId = booking.BookingId,
                    ClientName = client.FullName,
                    SessionType = session.SessionType,
                    SessionDate = session.Date,
                    SessionTime = session.Time,
                    LocationName = location.Name,
                    Amount = payment.Amount,
                    Method = payment.Method,
                    PaymentType = payment.PaymentType
                };

            return await query.ToListAsync();
        }
    }
}
