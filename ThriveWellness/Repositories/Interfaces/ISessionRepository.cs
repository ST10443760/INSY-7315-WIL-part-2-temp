using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    public interface ISessionRepository
    {
        Task<IEnumerable<Session>> GetAllAsync();
        Task<Session?> GetByIdAsync(int id);

        // All sessions (open, full and closed) with real booked counts, for
        // the public schedule page. Optional server-side filters.
        Task<IEnumerable<SessionListItemViewModel>> GetScheduleAsync(string? locationAddress, DateTime? date);

        // Active (non-cancelled) bookings against one session - used to
        // block/redirect a booking attempt on a session that's already full.
        Task<int> GetBookedCountAsync(int sessionId);
        Task AddAsync(Session session);
        Task UpdateAsync(Session session);
        Task DeleteAsync(int id);
        Task MarkAsFullAsync(int id);
        Task MarkAsOpenAsync(int id);
    }
}
