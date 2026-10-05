using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Repositories.Implementations
{
    // Repository pattern: thin EF Core wrapper around the Sessions table -
    // no business rules here (capacity/date validation lives in
    // SessionService), just queries and saves.
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

        public async Task<IEnumerable<SessionListItemViewModel>> GetScheduleAsync(string? locationAddress, DateTime? date)
        {
            // Public schedule only ever shows today-or-later sessions - past
            // sessions stay in the database (and still show in admin views,
            // which query Sessions directly rather than through this method)
            // for historical booking records, they just don't belong on the
            // client-facing schedule. This was never filtered before.
            var today = DateTime.Today;

            var query =
                from session in _context.Sessions
                join location in _context.Locations on session.LocationId equals location.LocationId
                where session.Date.Date >= today
                select new { session, location };

            if (!string.IsNullOrWhiteSpace(locationAddress))
            {
                query = query.Where(x => x.location.Address == locationAddress);
            }

            if (date.HasValue)
            {
                var day = date.Value.Date;
                query = query.Where(x => x.session.Date.Date == day);
            }

            var ordered = query.OrderBy(x => x.session.Date).ThenBy(x => x.session.Time);

            // Cancelled bookings don't count against capacity, matching the
            // capacity checks in BookingService and WaitlistService.
            return await ordered.Select(x => new SessionListItemViewModel
            {
                SessionId = x.session.SessionId,
                LocationName = x.location.Name,
                LocationAddress = x.location.Address,
                SessionType = x.session.SessionType,
                Date = x.session.Date,
                Time = x.session.Time,
                Capacity = x.session.Capacity,
                IsOpen = x.session.IsOpen,
                BookedCount = _context.Bookings.Count(b => b.SessionId == x.session.SessionId && b.Status != "Cancelled")
            }).ToListAsync();
        }

        public Task<int> GetBookedCountAsync(int sessionId)
        {
            return _context.Bookings.CountAsync(b => b.SessionId == sessionId && b.Status != "Cancelled");
        }

        public Task<bool> AnyForLocationAsync(int locationId)
        {
            return _context.Sessions.AnyAsync(s => s.LocationId == locationId);
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
