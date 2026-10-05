using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    // Repository pattern: the only place that queries or writes the
    // Locations table directly.
    public interface ILocationRepository
    {
        Task<IEnumerable<Location>> GetAllAsync();
        Task<Location?> GetByIdAsync(int id);
        Task AddAsync(Location location);
        Task UpdateAsync(Location location);
        Task DeleteAsync(int id);
    }
}
