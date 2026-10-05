using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: CRUD for class sessions, plus the two rules that make
    // capacity meaningful - a new session can't be dated in the past, and
    // reopening a closed session (MarkAsOpenAsync) immediately tries to pull
    // the next person off that session's waitlist (FR-11) rather than
    // waiting for a cancellation to trigger it.
    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IWaitlistService _waitlistService;

        public SessionService(ISessionRepository sessionRepository, IWaitlistService waitlistService)
        {
            _sessionRepository = sessionRepository;
            _waitlistService = waitlistService;
        }

        public Task<IEnumerable<Session>> GetAllAsync()
        {
            return _sessionRepository.GetAllAsync();
        }

        public Task<Session?> GetByIdAsync(int id)
        {
            return _sessionRepository.GetByIdAsync(id);
        }

        public Task<IEnumerable<SessionListItemViewModel>> GetScheduleAsync(string? locationAddress, DateTime? date)
        {
            return _sessionRepository.GetScheduleAsync(locationAddress, date);
        }

        public Task<int> GetBookedCountAsync(int sessionId)
        {
            return _sessionRepository.GetBookedCountAsync(sessionId);
        }

        // Creates a new session after validating capacity and checking the
        // date isn't in the past - sessions are for upcoming classes, not a
        // historical record.
        public Task CreateAsync(Session session)
        {
            Validate(session);

            if (session.Date.Date < DateTime.Today)
            {
                throw new ArgumentException("Session date cannot be in the past.");
            }

            return _sessionRepository.AddAsync(session);
        }

        // Updates an existing session - re-validates capacity, but (unlike
        // create) doesn't re-check the date, since an admin might
        // legitimately be touching a session that's already underway or in
        // the past (e.g. correcting its capacity after the fact).
        public Task UpdateAsync(Session session)
        {
            Validate(session);

            return _sessionRepository.UpdateAsync(session);
        }

        public Task DeleteAsync(int id)
        {
            return _sessionRepository.DeleteAsync(id);
        }

        public Task MarkAsFullAsync(int id)
        {
            return _sessionRepository.MarkAsFullAsync(id);
        }

        // Reopens a session an admin previously closed and immediately tries
        // to promote the next waitlisted client (FR-11) - without this,
        // reopening alone wouldn't actually fill the freed-up spot until
        // something else (like a cancellation) happened to trigger
        // PromoteNextInLineAsync.
        public async Task MarkAsOpenAsync(int id)
        {
            await _sessionRepository.MarkAsOpenAsync(id);
            await _waitlistService.PromoteNextInLineAsync(id);
        }

        // Shared validation for create and update: capacity has to be a
        // positive number, or nothing downstream (booking counts, the
        // waitlist) makes sense.
        private static void Validate(Session session)
        {
            if (session.Capacity <= 0)
            {
                throw new ArgumentException("Capacity must be greater than 0.");
            }
        }
    }
}
