using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface ISessionService
    {
        Task<IEnumerable<Session>> GetAllAsync();
        Task<Session?> GetByIdAsync(int id);
        Task<IEnumerable<Session>> GetAvailableSessionsAsync();
        Task CreateAsync(Session session);
        Task UpdateAsync(Session session);
        Task DeleteAsync(int id);
        Task MarkAsFullAsync(int id);
        Task MarkAsOpenAsync(int id);
    }
}
