using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    // Repository pattern: the only place that queries or writes the Clients
    // table directly.
    public interface IClientRepository
    {
        Task<Client?> GetByIdAsync(int id);
        Task<Client?> GetByEmailAsync(string email);
        Task AddAsync(Client client);
        Task UpdateAsync(Client client);

        // search: null/empty for every client, or a case-insensitive
        // substring match against name or email.
        Task<IEnumerable<ClientOverviewViewModel>> GetAllWithStatsAsync(string? search);
    }
}
