using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    // Repository pattern: the only place that queries or writes the Sessions
    // table directly.
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

        // Whether any session - past or future - is tied to this location.
        // Used to block deleting a location out from under its sessions.
        Task<bool> AnyForLocationAsync(int locationId);
        Task AddAsync(Session session);
        Task UpdateAsync(Session session);
        Task DeleteAsync(int id);
        Task MarkAsFullAsync(int id);
        Task MarkAsOpenAsync(int id);
    }
}
