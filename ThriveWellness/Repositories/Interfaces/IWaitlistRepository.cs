using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    public interface IWaitlistRepository
    {
        Task AddAsync(Waitlist waitlist);
        Task<IEnumerable<Waitlist>> GetBySessionAsync(int sessionId);
    }
}
