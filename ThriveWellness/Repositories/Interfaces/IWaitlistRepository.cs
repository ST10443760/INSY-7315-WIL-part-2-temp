using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    public interface IWaitlistRepository
    {
        Task AddAsync(Waitlist waitlist);
        Task<Waitlist?> GetByIdAsync(int id);
        Task<IEnumerable<Waitlist>> GetBySessionAsync(int sessionId);
        Task<Waitlist?> GetNextInLineAsync(int sessionId);
        Task RemoveAsync(int waitlistId);
        Task UpdateAsync(Waitlist waitlist);
    }
}
