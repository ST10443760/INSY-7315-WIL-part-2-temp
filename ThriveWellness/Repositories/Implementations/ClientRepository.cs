using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Repositories.Implementations
{
    public class ClientRepository : IClientRepository
    {
        private readonly ApplicationDbContext _context;

        public ClientRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Client?> GetByIdAsync(int id)
        {
            return await _context.Clients.FirstOrDefaultAsync(c => c.ClientId == id);
        }

        public async Task<Client?> GetByEmailAsync(string email)
        {
            return await _context.Clients.FirstOrDefaultAsync(c => c.Email == email);
        }

        public async Task AddAsync(Client client)
        {
            _context.Clients.Add(client);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Client client)
        {
            _context.Clients.Update(client);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<ClientOverviewViewModel>> GetAllWithStatsAsync(string? search)
        {
            IQueryable<Client> query = _context.Clients;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c =>
                    EF.Functions.ILike(c.FullName, $"%{term}%") ||
                    EF.Functions.ILike(c.Email, $"%{term}%"));
            }

            var ordered = query.OrderByDescending(c => c.CreatedAt);

            // Cancelled bookings don't count as a real session, matching the
            // capacity checks used elsewhere.
            return await ordered.Select(c => new ClientOverviewViewModel
            {
                ClientId = c.ClientId,
                FullName = c.FullName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                SessionCount = _context.Bookings.Count(b => b.ClientId == c.ClientId && b.Status != "Cancelled"),
                CreatedAt = c.CreatedAt,
                IsNew = c.IsNew
            }).ToListAsync();
        }
    }
}
