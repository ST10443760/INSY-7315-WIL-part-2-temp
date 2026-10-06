using Microsoft.Extensions.Logging;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;
using ThriveWellness.Services;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: manages the FIFO waitlist for a full session (FR-11) -
    // joining it, promoting someone into a real booking once a spot frees
    // up (automatically, or via an admin's explicit "Add to class"),
    // removing someone early, and sending a one-off "you're getting close"
    // courtesy email. Called by BookingService (to offer the waitlist when a
    // session is full), SessionService (when an admin reopens a closed
    // session), and directly by the admin waitlist screen.
    public class WaitlistService : IWaitlistService
    {
        private readonly IWaitlistRepository _waitlistRepository;
        // Depends on IBookingRepository directly rather than IBookingService:
        // BookingService depends on IWaitlistService (to promote on cancel),
        // so depending on IBookingService here would be circular.
        private readonly IBookingRepository _bookingRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IClientRepository _clientRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly INotificationService _notificationService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WaitlistService> _logger;

        public WaitlistService(
            IWaitlistRepository waitlistRepository,
            IBookingRepository bookingRepository,
            ISessionRepository sessionRepository,
            IClientRepository clientRepository,
            IPaymentRepository paymentRepository,
            INotificationService notificationService,
            ApplicationDbContext context,
            ILogger<WaitlistService> logger)
        {
            _waitlistRepository = waitlistRepository;
            _bookingRepository = bookingRepository;
            _sessionRepository = sessionRepository;
            _clientRepository = clientRepository;
            _paymentRepository = paymentRepository;
            _notificationService = notificationService;
            _context = context;
            _logger = logger;
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
        // first person off the waitlist and promotes them. Does nothing if
        // the queue is empty, the session is closed, or (see the capacity
        // guard below) a spot didn't actually free up. These guards are
        // unchanged from before - only the promotion itself (see
        // PromoteEntryAsync) now also creates a Pending payment, matching
        // the admin's manual "Add to class" button.
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

            var client = await _clientRepository.GetByIdAsync(next.ClientId);
            await PromoteEntryAsync(next, client);
        }

        // Admin-triggered: moves one specific waitlist entry into a real,
        // Awaiting-Payment booking with a Pending payment, on demand, rather
        // than waiting for a cancellation to free a spot automatically.
        // Unlike PromoteNextInLineAsync, this isn't limited to the entry at
        // the front of the queue - the admin picks the row.
        public async Task<AddToClassResult> AddToClassAsync(int waitlistId)
        {
            var entry = await _waitlistRepository.GetByIdAsync(waitlistId);
            if (entry == null)
            {
                return Fail("This waitlist entry no longer exists.");
            }

            var client = await _clientRepository.GetByIdAsync(entry.ClientId);
            if (client == null)
            {
                return Fail("This client could not be found.");
            }

            var session = await _sessionRepository.GetByIdAsync(entry.SessionId);
            if (session == null)
            {
                return Fail("This session could not be found.");
            }

            if (session.Date.Date < DateTime.Today)
            {
                return Fail("This session is in the past.");
            }

            if (!session.IsOpen)
            {
                return Fail("This session is closed. Reopen it before adding anyone to it.");
            }

            var sessionBookings = (await _bookingRepository.GetBookingsBySessionAsync(entry.SessionId))
                .Where(b => b.Status != "Cancelled")
                .ToList();

            if (sessionBookings.Count >= session.Capacity)
            {
                return Fail("This session is full. Raise its capacity or cancel a booking first.");
            }

            if (sessionBookings.Any(b => b.ClientId == entry.ClientId))
            {
                return Fail($"{client.FullName} already has an active booking for this session.");
            }

            var (booking, emailSent) = await PromoteEntryAsync(entry, client);

            return new AddToClassResult { Success = true, EmailSent = emailSent };
        }

        private static AddToClassResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        // The shared promotion routine behind both PromoteNextInLineAsync
        // and AddToClassAsync (Part 3): the caller is responsible for
        // deciding *whether* this entry should be promoted (its own capacity
        // guard, since the two callers' rules genuinely differ - see each
        // method above); this just does the promotion itself, the same way
        // every time. Creating the Booking, creating its Payment, removing
        // the waitlist entry and renumbering the rest all happen in one
        // transaction, rolled back on any error - a half-promoted entry
        // (booked but still queued, or queued but double-booked) would be
        // worse than the promotion simply not having happened.
        //
        // The payment-details email is sent only after that transaction has
        // committed, and a failed send does not undo the booking - the
        // booking and its Pending payment are real either way, so the
        // caller is told via the returned EmailSent flag rather than having
        // the whole promotion fail over an email provider hiccup.
        private async Task<(Booking Booking, bool EmailSent)> PromoteEntryAsync(Waitlist entry, Client? client)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            Booking booking;
            try
            {
                await _waitlistRepository.RemoveAsync(entry.WaitlistId);

                var remaining = (await _waitlistRepository.GetBySessionAsync(entry.SessionId))
                    .Where(w => w.Position > entry.Position);
                foreach (var later in remaining)
                {
                    later.Position -= 1;
                    await _waitlistRepository.UpdateAsync(later);
                }

                booking = new Booking
                {
                    ClientId = entry.ClientId,
                    SessionId = entry.SessionId,
                    BookingDate = DateTime.UtcNow,
                    Status = "Awaiting Payment",
                    CancellationToken = CancellationTokenGenerator.Generate()
                };
                await _bookingRepository.CreateBookingAsync(booking);

                // Defaults: the client's own stored plan if it's one of the
                // two the booking form itself offers, otherwise per-class;
                // EFT, since that's what an admin follows up with details
                // for (cash needs no advance details). The amount always
                // comes from PaymentPricing - the R120/R450 figures are
                // never retyped here.
                var paymentType = client?.PaymentType is "per-class" or "monthly"
                    ? client.PaymentType
                    : "per-class";
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Method = "EFT",
                    Amount = PaymentPricing.GetAmountForPaymentType(paymentType),
                    Status = "Pending",
                    PaymentType = paymentType
                };
                await _paymentRepository.CreateAsync(payment);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            var emailSent = true;
            try
            {
                await _notificationService.SendWaitlistNotificationAsync(booking);
            }
            catch (Exception ex)
            {
                emailSent = false;
                _logger.LogError(ex,
                    "Failed to send the payment-details email for booking {BookingId} after promoting waitlist entry {WaitlistId} - the booking itself was not rolled back.",
                    booking.BookingId, entry.WaitlistId);
            }

            return (booking, emailSent);
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
        // automatic email PromoteEntryAsync sends once they're actually off
        // the list.
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
