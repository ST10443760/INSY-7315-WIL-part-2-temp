using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;
using ThriveWellness.Services;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: owns the booking lifecycle end to end - creating a
    // booking for the signed-in client
    // (with its intake form and pending payment) or routing to the waitlist
    // when a session is full, and cancelling a booking either by the
    // client's own link (FR-18) or by an admin (FR-12). Called by
    // BookingController; never touched directly by a view. Sits between the
    // controller and the repositories so capacity checks, pricing and the
    // first-booking intake rule all live in exactly one place.
    public class BookingService : IBookingService
    {
        private readonly IClientRepository _clientRepository;
        private readonly IBookingRepository _bookingRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IWaitlistService _waitlistService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly INotificationService _notificationService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BookingService> _logger;

        public BookingService(
            IClientRepository clientRepository,
            IBookingRepository bookingRepository,
            ISessionRepository sessionRepository,
            IWaitlistService waitlistService,
            IPaymentRepository paymentRepository,
            INotificationService notificationService,
            ApplicationDbContext context,
            ILogger<BookingService> logger)
        {
            _clientRepository = clientRepository;
            _bookingRepository = bookingRepository;
            _sessionRepository = sessionRepository;
            _waitlistService = waitlistService;
            _paymentRepository = paymentRepository;
            _notificationService = notificationService;
            _context = context;
            _logger = logger;
        }

        // Whether this client has never booked before - their first booking
        // is the one that needs the intake form and consent (FR-05). Any
        // booking counts, cancelled or not, since its intake form was still
        // filled in.
        public async Task<bool> IsFirstBookingAsync(int clientId)
        {
            return !(await _bookingRepository.GetBookingsByClientAsync(clientId)).Any();
        }

        // Creates a booking end to end for the signed-in client (the
        // controller passes their ClientId from the sign-in cookie, never
        // from the form): requires consent on their first booking, saves any
        // edit to their name or phone number, checks the session has room,
        // creates the booking and its pending payment (plus an intake form on
        // a first booking), and sends the booking received email. Returns a
        // failure if the client or session doesn't exist or consent is
        // missing; returns RequiresWaitlist - without creating a booking - if
        // the session is full or closed, so the caller can offer the waitlist
        // instead (FR-11).
        public async Task<BookingCreateResult> CreateBookingAsync(BookingRequest request)
        {
            var client = await _clientRepository.GetByIdAsync(request.ClientId);
            if (client == null)
            {
                return new BookingCreateResult { Success = false, ErrorMessage = "We couldn't find your account. Please sign in again." };
            }

            var isFirstBooking = await IsFirstBookingAsync(client.ClientId);

            if (isFirstBooking && !request.ConsentSigned)
            {
                return new BookingCreateResult
                {
                    Success = false,
                    ErrorMessage = "Consent must be given to complete the intake form."
                };
            }

            // The details form is pre-filled from the client's account but
            // editable - save any edit to name or phone here, since nothing
            // else in this flow does (a returning client's edited phone used
            // to be lost). Email comes from their Google account, so it's
            // never touched. A first booking also records the client's
            // payment plan, as the old new-client path did; after that
            // PaymentType is a per-booking choice, not a client detail. A
            // client who already booked before stops being "new" - that's
            // what drives the first-timer location email. Trimmed and
            // compared against the stored values so a confirmed, unedited
            // submission never writes anything.
            var trimmedFullName = request.FullName.Trim();
            var trimmedPhoneNumber = request.PhoneNumber.Trim();
            var changed = false;

            if (trimmedFullName != client.FullName || trimmedPhoneNumber != client.PhoneNumber)
            {
                client.FullName = trimmedFullName;
                client.PhoneNumber = trimmedPhoneNumber;
                changed = true;
            }

            if (isFirstBooking && string.IsNullOrEmpty(client.PaymentType))
            {
                client.PaymentType = request.PaymentType;
                changed = true;
            }

            if (!isFirstBooking && client.IsNew)
            {
                client.IsNew = false;
                changed = true;
            }

            if (changed)
            {
                await _clientRepository.UpdateAsync(client);
            }

            var session = await _sessionRepository.GetByIdAsync(request.SessionId);
            if (session == null)
            {
                return new BookingCreateResult { Success = false, ErrorMessage = "Session not found." };
            }

            var activeBookings = (await _bookingRepository.GetBookingsBySessionAsync(request.SessionId))
                .Count(b => b.Status != "Cancelled");

            // Closed or at/over capacity - either way there's no room, so the
            // caller gets routed to the waitlist instead of a booking.
            if (!session.IsOpen || activeBookings >= session.Capacity)
            {
                // The client (found or just created above) already exists at
                // this point, so the caller can join them to the waitlist
                // without repeating the lookup/creation logic.
                return new BookingCreateResult { Success = false, RequiresWaitlist = true, ClientId = client.ClientId };
            }

            var cancellationToken = CancellationTokenGenerator.Generate();

            var booking = new Booking
            {
                ClientId = client.ClientId,
                SessionId = request.SessionId,
                BookingDate = DateTime.UtcNow,
                Status = "Awaiting Payment",
                CancellationToken = cancellationToken
            };
            await _bookingRepository.CreateBookingAsync(booking);

            var payment = new Payment
            {
                BookingId = booking.BookingId,
                Method = request.Method,
                Amount = PaymentPricing.GetAmountForPaymentType(request.PaymentType),
                Status = "Pending",
                PaymentType = request.PaymentType
            };
            await _paymentRepository.CreateAsync(payment);

            if (isFirstBooking)
            {
                var intakeForm = new IntakeForm
                {
                    BookingId = booking.BookingId,
                    MedicalNotes = request.MedicalNotes ?? string.Empty,
                    ConsentSigned = request.ConsentSigned,
                    SubmittedAt = DateTime.UtcNow
                };
                _context.IntakeForms.Add(intakeForm);
                await _context.SaveChangesAsync();
            }

            // The booking, payment and intake form are already saved, so an
            // email failure must never turn into an error page or a lost
            // booking: NotificationService logs it as a Failed notification
            // (visible on the admin Emails page), and the client still lands
            // on the normal Booking received page.
            try
            {
                await _notificationService.SendConfirmationEmailAsync(booking);
            }
            catch (Exception ex)
            {
                LogEmailFailure(ex, "booking received", booking.BookingId);
            }

            return new BookingCreateResult
            {
                Success = true,
                BookingId = booking.BookingId,
                ClientId = client.ClientId,
                CancellationToken = cancellationToken
            };
        }

        // Client-facing cancellation entry point: looks the booking up by its
        // single-use cancellation token (FR-18), since the link in the
        // confirmation/waitlist emails is the only thing a client has to
        // cancel with - there's no login for them to go through. Fails with
        // a generic "invalid link" message rather than hinting at whether a
        // token almost matched.
        public async Task<CancelBookingResult> CancelBookingAsync(string cancellationToken)
        {
            var booking = await _bookingRepository.GetByCancellationTokenAsync(cancellationToken);
            if (booking == null)
            {
                return new CancelBookingResult { Success = false, ErrorMessage = "Invalid cancellation link." };
            }

            // Client-initiated: no cancellation email needed, they already
            // know (they just clicked the link).
            return await CancelAsync(booking, notifyClient: false);
        }

        // Admin-facing cancellation entry point (FR-12): looks the booking up
        // by its database id, since an admin is acting from the dashboard,
        // not a link.
        public async Task<CancelBookingResult> CancelByAdminAsync(int bookingId)
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking == null)
            {
                return new CancelBookingResult { Success = false, ErrorMessage = "Booking not found." };
            }

            // Admin-initiated (FR-12): the client didn't do this themselves,
            // so they need to be told.
            return await CancelAsync(booking, notifyClient: true);
        }

        // Shared cancellation logic for both entry points above: flips the
        // booking to Cancelled, frees its slot for the waitlist, and only
        // emails the client when an admin did the cancelling - a client who
        // clicked their own cancellation link already knows.
        private async Task<CancelBookingResult> CancelAsync(Booking booking, bool notifyClient)
        {
            if (booking.Status == "Cancelled")
            {
                return new CancelBookingResult { Success = false, ErrorMessage = "This booking has already been cancelled." };
            }

            booking.Status = "Cancelled";
            await _bookingRepository.UpdateAsync(booking);

            await _waitlistService.PromoteNextInLineAsync(booking.SessionId);

            if (notifyClient)
            {
                // The cancellation itself already stands; a failed email is
                // logged (and recorded as Failed) rather than shown as an
                // error.
                try
                {
                    await _notificationService.SendCancellationEmailAsync(booking);
                }
                catch (Exception ex)
                {
                    LogEmailFailure(ex, "cancellation", booking.BookingId);
                }
            }

            return new CancelBookingResult { Success = true };
        }

        // Exception type and message only - SendGrid's error text never
        // contains the API key, and nothing from configuration is logged.
        private void LogEmailFailure(Exception ex, string emailKind, int bookingId)
        {
            _logger.LogWarning(
                "The {EmailKind} email for booking {BookingId} could not be sent ({ExceptionType}: {ExceptionMessage}). The booking itself was saved.",
                emailKind, bookingId, ex.GetType().Name, ex.Message);
        }

        public Task<Booking?> GetByIdAsync(int bookingId)
        {
            return _bookingRepository.GetByIdAsync(bookingId);
        }

        // Every booking belonging to one client, newest session first, for
        // their dashboard. Left-joins Payments since a booking's payment row
        // is expected but shouldn't hide the booking if it were missing.
        public async Task<IReadOnlyList<ClientBookingViewModel>> GetClientBookingsAsync(int clientId)
        {
            return await (
                from booking in _context.Bookings
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                join location in _context.Locations on session.LocationId equals location.LocationId
                join payment in _context.Payments on booking.BookingId equals payment.BookingId into payments
                from payment in payments.DefaultIfEmpty()
                where booking.ClientId == clientId
                orderby session.Date descending, session.Time descending
                select new ClientBookingViewModel
                {
                    BookingId = booking.BookingId,
                    SessionType = session.SessionType,
                    SessionDate = session.Date,
                    SessionTime = session.Time,
                    LocationAddress = location.Address,
                    Status = booking.Status,
                    PaymentStatus = payment != null ? payment.Status : null,
                    Amount = payment != null ? payment.Amount : null,
                    CancellationToken = booking.CancellationToken
                }).ToListAsync();
        }
    }
}
