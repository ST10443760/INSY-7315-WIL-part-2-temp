using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    public interface ILocationRepository
    {
        Task<IEnumerable<Location>> GetAllAsync();
        Task<Location?> GetByIdAsync(int id);
    }
}
