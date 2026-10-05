using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    // Repository pattern: the only place that queries or writes the Bookings
    // table directly. BookingService and WaitlistService depend on this
    // interface rather than EF Core's DbSet, so that query shape can change
    // without touching the business rules that call it.
    public interface IBookingRepository
    {
        Task<Booking?> GetByIdAsync(int id);

        // Looks a booking up by its FR-18 cancellation token rather than its
        // id - the only lookup a client (who never logs in) can perform.
        Task<Booking?> GetByCancellationTokenAsync(string token);
        Task<IEnumerable<Booking>> GetBookingsBySessionAsync(int sessionId);
        Task<IEnumerable<Booking>> GetBookingsByClientAsync(int clientId);
        Task CreateBookingAsync(Booking booking);
        Task UpdateAsync(Booking booking);
    }
}
