using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;
using ThriveWellness.Services;

namespace ThriveWellness.Services.Implementations
{
    public class WaitlistService : IWaitlistService
    {
        private readonly IWaitlistRepository _waitlistRepository;
        // Depends on IBookingRepository directly rather than IBookingService:
        // BookingService depends on IWaitlistService (to promote on cancel),
        // so depending on IBookingService here would be circular.
        private readonly IBookingRepository _bookingRepository;

        public WaitlistService(IWaitlistRepository waitlistRepository, IBookingRepository bookingRepository)
        {
            _waitlistRepository = waitlistRepository;
            _bookingRepository = bookingRepository;
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

        public async Task PromoteNextInLineAsync(int sessionId)
        {
            var next = await _waitlistRepository.GetNextInLineAsync(sessionId);
            if (next == null)
            {
                return;
            }

            await _waitlistRepository.RemoveAsync(next.WaitlistId);

            var remaining = (await _waitlistRepository.GetBySessionAsync(sessionId))
                .Where(w => w.Position > next.Position);
            foreach (var entry in remaining)
            {
                entry.Position -= 1;
                await _waitlistRepository.UpdateAsync(entry);
            }

            var booking = new Booking
            {
                ClientId = next.ClientId,
                SessionId = sessionId,
                BookingDate = DateTime.UtcNow,
                Status = "Awaiting Payment",
                CancellationToken = CancellationTokenGenerator.Generate()
            };
            await _bookingRepository.CreateBookingAsync(booking);
        }
    }
}
