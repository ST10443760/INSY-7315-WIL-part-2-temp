using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    public interface ISessionRepository
    {
        Task<IEnumerable<Session>> GetAllAsync();
        Task<Session?> GetByIdAsync(int id);
        Task<IEnumerable<Session>> GetAvailableSessionsAsync();
        Task AddAsync(Session session);
        Task UpdateAsync(Session session);
        Task DeleteAsync(int id);
        Task MarkAsFullAsync(int id);
        Task MarkAsOpenAsync(int id);
    }
}
