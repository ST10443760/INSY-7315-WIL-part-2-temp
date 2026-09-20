using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    public class WaitlistService : IWaitlistService
    {
        private readonly IWaitlistRepository _waitlistRepository;

        public WaitlistService(IWaitlistRepository waitlistRepository)
        {
            _waitlistRepository = waitlistRepository;
        }

        public async Task<Waitlist> JoinWaitlistAsync(int clientId, int sessionId)
        {
            var existing = (await _waitlistRepository.GetBySessionAsync(sessionId)).ToList();
            var nextPosition = existing.Count == 0 ? 1 : existing.Max(w => w.Position) + 1;

            var entry = new Waitlist
            {
                ClientId = clientId,
                SessionId = sessionId,
                Position = nextPosition,
                DateAdded = DateTime.UtcNow
            };

            await _waitlistRepository.AddAsync(entry);
            return entry;
        }
    }
}
