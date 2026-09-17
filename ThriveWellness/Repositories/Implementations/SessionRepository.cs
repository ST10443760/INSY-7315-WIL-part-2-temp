using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Repositories.Implementations
{
    public class SessionRepository : ISessionRepository
    {
        private readonly ApplicationDbContext _context;

        public SessionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Session>> GetAllAsync()
        {
            return await _context.Sessions.ToListAsync();
        }

        public async Task<Session?> GetByIdAsync(int id)
        {
            return await _context.Sessions.FirstOrDefaultAsync(s => s.SessionId == id);
        }

        public async Task<IEnumerable<Session>> GetAvailableSessionsAsync()
        {
            return await _context.Sessions
                .Where(s => s.IsOpen &&
                    _context.Bookings.Count(b => b.SessionId == s.SessionId && b.Status != "cancelled") < s.Capacity)
                .ToListAsync();
        }

        public async Task AddAsync(Session session)
        {
            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Session session)
        {
            _context.Sessions.Update(session);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.SessionId == id);
            if (session != null)
            {
                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAsFullAsync(int id)
        {
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.SessionId == id);
            if (session != null)
            {
                session.IsOpen = false;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAsOpenAsync(int id)
        {
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.SessionId == id);
            if (session != null)
            {
                session.IsOpen = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}
