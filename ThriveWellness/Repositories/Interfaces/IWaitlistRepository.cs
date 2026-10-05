using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    // Repository pattern: the only place that queries or writes the
    // Waitlists table directly.
    public interface IWaitlistRepository
    {
        Task AddAsync(Waitlist waitlist);
        Task<Waitlist?> GetByIdAsync(int id);
        Task<IEnumerable<Waitlist>> GetBySessionAsync(int sessionId);

        // The entry with the lowest Position for a session - the front of
        // the FIFO queue, and who PromoteNextInLineAsync acts on.
        Task<Waitlist?> GetNextInLineAsync(int sessionId);
        Task RemoveAsync(int waitlistId);
        Task UpdateAsync(Waitlist waitlist);
    }
}
