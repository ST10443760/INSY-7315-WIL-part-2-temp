using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;
using ThriveWellness.Services;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: manages the FIFO waitlist for a full session (FR-11) -
    // joining it, promoting the next person in line into a real booking once
    // a spot frees up, removing someone early, and sending a one-off
    // "you're getting close" courtesy email. Called by BookingService (to
    // offer the waitlist when a session is full), SessionService (when an
    // admin reopens a closed session), and directly by the admin waitlist
    // screen.
    public class WaitlistService : IWaitlistService
    {
        private readonly IWaitlistRepository _waitlistRepository;
        // Depends on IBookingRepository directly rather than IBookingService:
        // BookingService depends on IWaitlistService (to promote on cancel),
        // so depending on IBookingService here would be circular.
        private readonly IBookingRepository _bookingRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly INotificationService _notificationService;

        public WaitlistService(
            IWaitlistRepository waitlistRepository,
            IBookingRepository bookingRepository,
            ISessionRepository sessionRepository,
            INotificationService notificationService)
        {
            _waitlistRepository = waitlistRepository;
            _bookingRepository = bookingRepository;
            _sessionRepository = sessionRepository;
            _notificationService = notificationService;
        }

        // Adds a client to the back of the queue for a session (FR-11):
        // position is just "one more than the current maximum", so a
        // mid-queue removal (see RemoveAsync below) never has to leave a gap
        // for a later join to worry about.
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

        // Called whenever a spot might have opened up (a booking
        // cancellation, or an admin reopening a closed session): takes the
        // first person off the waitlist, turns them into a real booking, and
        // shifts everyone behind them up one position. Does nothing if the
        // queue is empty, the session is closed, or (see the capacity guard
        // below) a spot didn't actually free up.
        public async Task PromoteNextInLineAsync(int sessionId)
        {
            var next = await _waitlistRepository.GetNextInLineAsync(sessionId);
            if (next == null)
            {
                return;
            }

            var session = await _sessionRepository.GetByIdAsync(sessionId);
            if (session == null || !session.IsOpen)
            {
                return;
            }

            var activeBookings = (await _bookingRepository.GetBookingsBySessionAsync(sessionId))
                .Count(b => b.Status != "Cancelled");
            if (activeBookings >= session.Capacity)
            {
                // No room yet - leave the queue as-is. Reached from
                // MarkAsOpenAsync, which doesn't guarantee a freed spot the
                // way a cancellation does.
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

            await _notificationService.SendWaitlistNotificationAsync(booking);
        }

        // Removes a single waitlist entry (e.g. an admin manually clearing
        // someone) and shifts everyone behind them up one position, so the
        // ordering never ends up with a gap in it.
        public async Task RemoveAsync(int waitlistId)
        {
            var entry = await _waitlistRepository.GetByIdAsync(waitlistId);
            if (entry == null)
            {
                return;
            }

            await _waitlistRepository.RemoveAsync(waitlistId);

            var remaining = (await _waitlistRepository.GetBySessionAsync(entry.SessionId))
                .Where(w => w.Position > entry.Position);
            foreach (var later in remaining)
            {
                later.Position -= 1;
                await _waitlistRepository.UpdateAsync(later);
            }
        }

        // Sends the one-off "you're getting close, a spot may open soon"
        // courtesy email for a specific waitlist entry - distinct from the
        // automatic email PromoteNextInLineAsync sends once they're actually
        // off the list.
        public async Task NotifyAsync(int waitlistId)
        {
            var entry = await _waitlistRepository.GetByIdAsync(waitlistId);
            if (entry == null)
            {
                return;
            }

            await _notificationService.SendWaitlistCourtesyEmailAsync(entry);
        }
    }
}
