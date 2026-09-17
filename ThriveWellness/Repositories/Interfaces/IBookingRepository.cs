using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    public interface IBookingRepository
    {
        Task<Booking?> GetByIdAsync(int id);
        Task<Booking?> GetByCancellationTokenAsync(string token);
        Task<IEnumerable<Booking>> GetBookingsBySessionAsync(int sessionId);
        Task<IEnumerable<Booking>> GetBookingsByClientAsync(int clientId);
        Task CreateBookingAsync(Booking booking);
        Task UpdateAsync(Booking booking);
    }
}
