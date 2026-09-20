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
    }
}
