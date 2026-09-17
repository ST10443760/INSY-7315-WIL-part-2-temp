using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Repositories.Implementations
{
    public class BookingRepository : IBookingRepository
    {
        private readonly ApplicationDbContext _context;

        public BookingRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Booking?> GetByIdAsync(int id)
        {
            return await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == id);
        }

        public async Task<Booking?> GetByCancellationTokenAsync(string token)
        {
            return await _context.Bookings.FirstOrDefaultAsync(b => b.CancellationToken == token);
        }

        public async Task<IEnumerable<Booking>> GetBookingsBySessionAsync(int sessionId)
        {
            return await _context.Bookings.Where(b => b.SessionId == sessionId).ToListAsync();
        }

        public async Task<IEnumerable<Booking>> GetBookingsByClientAsync(int clientId)
        {
            return await _context.Bookings.Where(b => b.ClientId == clientId).ToListAsync();
        }

        public async Task CreateBookingAsync(Booking booking)
        {
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Booking booking)
        {
            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();
        }
    }
}
