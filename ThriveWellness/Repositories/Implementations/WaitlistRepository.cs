using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Repositories.Implementations
{
    public class WaitlistRepository : IWaitlistRepository
    {
        private readonly ApplicationDbContext _context;

        public WaitlistRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Waitlist waitlist)
        {
            _context.Waitlists.Add(waitlist);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Waitlist>> GetBySessionAsync(int sessionId)
        {
            return await _context.Waitlists
                .Where(w => w.SessionId == sessionId)
                .OrderBy(w => w.Position)
                .ToListAsync();
        }

        public async Task<Waitlist?> GetNextInLineAsync(int sessionId)
        {
            return await _context.Waitlists
                .Where(w => w.SessionId == sessionId)
                .OrderBy(w => w.Position)
                .FirstOrDefaultAsync();
        }

        public async Task RemoveAsync(int waitlistId)
        {
            var entry = await _context.Waitlists.FirstOrDefaultAsync(w => w.WaitlistId == waitlistId);
            if (entry != null)
            {
                _context.Waitlists.Remove(entry);
                await _context.SaveChangesAsync();
            }
        }
    }
}
