using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;
using ThriveWellness.Services;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: owns the booking lifecycle end to end - checking whether
    // an email belongs to a new or returning client, creating a booking
    // (with its intake form and pending payment) or routing to the waitlist
    // when a session is full, and cancelling a booking either by the
    // client's own link (FR-18) or by an admin (FR-12). Called by
    // BookingController; never touched directly by a view. Sits between the
    // controller and the repositories so capacity checks, pricing and the
    // new-vs-returning client distinction all live in exactly one place.
    public class BookingService : IBookingService
    {
        private readonly IClientRepository _clientRepository;
        private readonly IBookingRepository _bookingRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IWaitlistService _waitlistService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly INotificationService _notificationService;
        private readonly ApplicationDbContext _context;

        public BookingService(
            IClientRepository clientRepository,
            IBookingRepository bookingRepository,
            ISessionRepository sessionRepository,
            IWaitlistService waitlistService,
            IPaymentRepository paymentRepository,
            INotificationService notificationService,
            ApplicationDbContext context)
        {
            _clientRepository = clientRepository;
            _bookingRepository = bookingRepository;
            _sessionRepository = sessionRepository;
            _waitlistService = waitlistService;
            _paymentRepository = paymentRepository;
            _notificationService = notificationService;
            _context = context;
        }

        // Looks up a client by email so the booking form can decide whether
        // to show the full intake form (new client) or just confirm details
        // (returning client) - this is the "returning client" detection used
        // by the first step of the booking flow.
        public async Task<ClientStatusResult> CheckClientStatusAsync(string email)
        {
            var client = await _clientRepository.GetByEmailAsync(email);

            if (client == null)
            {
                return new ClientStatusResult { IsNew = true };
            }

            return new ClientStatusResult
            {
                IsNew = false,
                FullName = client.FullName,
                PhoneNumber = client.PhoneNumber
            };
        }

        // Creates a booking end to end: finds or creates the client,
        // requires consent for a brand-new client, checks the session has
        // room, creates the booking and its pending payment (plus an intake
        // form for new clients), and sends the confirmation email. Returns a
        // failure if consent is missing or the session doesn't exist; returns
        // RequiresWaitlist - without creating anything - if the session is
        // full or closed, so the caller can offer the waitlist instead
        // (FR-11).
        public async Task<BookingCreateResult> CreateBookingAsync(BookingRequest request)
        {
            var client = await _clientRepository.GetByEmailAsync(request.Email);
            var isNewClient = client == null;

            if (isNewClient)
            {
                if (!request.ConsentSigned)
                {
                    return new BookingCreateResult
                    {
                        Success = false,
                        ErrorMessage = "Consent must be given to complete the intake form."
                    };
                }

                client = new Client
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    IsNew = true,
                    PaymentType = request.PaymentType,
                    CreatedAt = DateTime.UtcNow
                };
                await _clientRepository.AddAsync(client);
            }
            else if (client!.IsNew)
            {
                client.IsNew = false;
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

            if (isNewClient)
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

            await _notificationService.SendConfirmationEmailAsync(booking);

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
                await _notificationService.SendCancellationEmailAsync(booking);
            }

            return new CancelBookingResult { Success = true };
        }

        public Task<Booking?> GetByIdAsync(int bookingId)
        {
            return _bookingRepository.GetByIdAsync(bookingId);
        }
    }
}
