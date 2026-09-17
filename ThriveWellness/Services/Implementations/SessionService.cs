using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _sessionRepository;

        public SessionService(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public Task<IEnumerable<Session>> GetAllAsync()
        {
            return _sessionRepository.GetAllAsync();
        }

        public Task<Session?> GetByIdAsync(int id)
        {
            return _sessionRepository.GetByIdAsync(id);
        }

        public Task<IEnumerable<Session>> GetAvailableSessionsAsync()
        {
            return _sessionRepository.GetAvailableSessionsAsync();
        }

        public Task CreateAsync(Session session)
        {
            Validate(session);

            if (session.Date.Date < DateTime.Today)
            {
                throw new ArgumentException("Session date cannot be in the past.");
            }

            return _sessionRepository.AddAsync(session);
        }

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

        public Task MarkAsOpenAsync(int id)
        {
            return _sessionRepository.MarkAsOpenAsync(id);
        }

        private static void Validate(Session session)
        {
            if (session.Capacity <= 0)
            {
                throw new ArgumentException("Capacity must be greater than 0.");
            }
        }
    }
}
