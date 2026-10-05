using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: builds and sends every client-facing email the system
    // sends - booking confirmation (including the FR-18 cancellation link),
    // welcome, reminder, first-timer location, waitlist promotion, waitlist
    // courtesy, and cancellation - and logs each send as a Notification row
    // so ScheduledNotificationService can tell what's already gone out.
    // Also the observer half of the Observer pattern: OnPaymentConfirmed is
    // wired up to IPaymentService.PaymentConfirmed from Program.cs rather
    // than from this class's own constructor (see that method's comment for
    // why).
    public class NotificationService : INotificationService
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IClientRepository _clientRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly string _appBaseUrl;
        private readonly PaymentOptions _paymentOptions;
        private readonly ILogger<NotificationService> _logger;

        // AppBaseUrl has to be configured for cancellation links (FR-18) to
        // point anywhere real, so this fails fast at startup rather than
        // emailing a client a broken link later. PaymentOptions is treated
        // differently (see BuildPaymentDetailsHtml below) - an unconfigured
        // bank account is far less broken than an unconfigured app URL, so
        // it degrades gracefully per email instead of failing startup.
        public NotificationService(
            IBookingRepository bookingRepository,
            IClientRepository clientRepository,
            ISessionRepository sessionRepository,
            ILocationRepository locationRepository,
            IPaymentRepository paymentRepository,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IConfiguration configuration,
            IOptions<PaymentOptions> paymentOptions,
            ILogger<NotificationService> logger)
        {
            _bookingRepository = bookingRepository;
            _clientRepository = clientRepository;
            _sessionRepository = sessionRepository;
            _locationRepository = locationRepository;
            _paymentRepository = paymentRepository;
            _emailSender = emailSender;
            _context = context;
            _appBaseUrl = configuration["AppBaseUrl"]?.TrimEnd('/')
                ?? throw new InvalidOperationException("AppBaseUrl is not configured.");
            _paymentOptions = paymentOptions.Value;
            _logger = logger;
        }

        // Observer: this is wired up to IPaymentService.PaymentConfirmed as a
        // registration step in Program.cs (the composition root), rather than
        // in this constructor - subscribing here would mean NotificationService
        // depends on IPaymentService, whose own DI registration needs to
        // resolve INotificationService to force this subscription to exist,
        // which is a circular dependency.
        public void OnPaymentConfirmed(object? sender, PaymentConfirmedEventArgs e)
        {
            // Block rather than fire-and-forget: the repositories here share
            // the same Scoped DbContext as the rest of this request. A
            // detached fire-and-forget task races the request's own
            // completion (and the DbContext disposal that follows it),
            // which produced real "another read operation is pending" /
            // connection-aborted errors when this ran undetached.
            HandlePaymentConfirmedAsync(e).GetAwaiter().GetResult();
        }

        // Only sends the welcome email if the booking still exists -
        // defensive against a payment somehow outliving its booking.
        private async Task HandlePaymentConfirmedAsync(PaymentConfirmedEventArgs e)
        {
            var booking = await _bookingRepository.GetByIdAsync(e.BookingId);
            if (booking != null)
            {
                await SendWelcomeEmailAsync(booking);
            }
        }

        // Sent once, right after a brand-new client's first payment is
        // confirmed (via the Observer subscription above) - covers class
        // details and what to bring, since this is their very first session
        // with us.
        public async Task SendWelcomeEmailAsync(Booking booking)
        {
            var client = await _clientRepository.GetByIdAsync(booking.ClientId);
            if (client == null)
            {
                _logger.LogWarning("SendWelcomeEmailAsync: client {ClientId} not found for booking {BookingId}", booking.ClientId, booking.BookingId);
                return;
            }

            var session = await _sessionRepository.GetByIdAsync(booking.SessionId);
            var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;

            var html = $"""
                <p>Hi {client.FullName},</p>
                <p>Welcome to Thrive Wellness! Here are the details for your first class:</p>
                <ul>
                    <li><strong>Class:</strong> {session?.SessionType}</li>
                    <li><strong>Date:</strong> {session?.Date.ToString("yyyy-MM-dd")}</li>
                    <li><strong>Time:</strong> {session?.Time.ToString(@"hh\:mm")}</li>
                    <li><strong>Location:</strong> {location?.Name} - {location?.Address}</li>
                </ul>
                <p><strong>Before you arrive:</strong> please arrive 10 minutes early, wear comfortable
                workout clothing, and bring a mat and water bottle if you have them.</p>
                <p>See you soon!</p>
                """;

            await _emailSender.SendEmailAsync(client.Email, "Welcome to Thrive Wellness", html);
            await WriteNotificationRecordAsync(booking.BookingId, "Welcome");
        }

        // Sent the moment a booking is created (before payment), covering
        // the class details, the amount owing and how to pay, and the FR-18
        // cancellation link - this is often the only email a client gets
        // before turning up, so it has to be self-contained.
        public async Task SendConfirmationEmailAsync(Booking booking)
        {
            var client = await _clientRepository.GetByIdAsync(booking.ClientId);
            if (client == null)
            {
                _logger.LogWarning("SendConfirmationEmailAsync: client {ClientId} not found for booking {BookingId}", booking.ClientId, booking.BookingId);
                return;
            }

            var session = await _sessionRepository.GetByIdAsync(booking.SessionId);
            var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;
            var payment = await _paymentRepository.GetByBookingIdAsync(booking.BookingId);
            var cancelUrl = $"{_appBaseUrl}/Booking/Cancel/{booking.CancellationToken}";
            var paymentDetails = BuildPaymentDetailsHtml(client, booking, payment);

            var html = $"""
                <p>Hi {client.FullName},</p>
                <p>Your booking is in. Here are the details:</p>
                <ul>
                    <li><strong>Class:</strong> {session?.SessionType}</li>
                    <li><strong>Date:</strong> {session?.Date.ToString("yyyy-MM-dd")}</li>
                    <li><strong>Time:</strong> {session?.Time.ToString(@"hh\:mm")}</li>
                    <li><strong>Venue:</strong> {location?.Name} - {location?.Address}</li>
                </ul>
                {paymentDetails}
                <p>Need to cancel? <a href="{cancelUrl}">Cancel this booking</a>.</p>
                """;

            await _emailSender.SendEmailAsync(client.Email, "Your Thrive Wellness booking is confirmed", html);
            await WriteNotificationRecordAsync(booking.BookingId, "Confirmation");
        }

        // Builds the confirmation email's payment block: the amount owing
        // (payment.Amount - already computed from the shared PaymentPricing
        // constants when this Payment row was created in BookingService, so
        // there's no need to re-derive R120/R450 here), the studio's real
        // banking details from configuration, and an EFT reference of the
        // client's name plus booking number. One item per line as plain
        // text (no table or image) so the account number can still be
        // copied on a phone. Cash is always mentioned as an alternative,
        // configured or not.
        //
        // If any of AccountHolder/Bank/AccountNumber isn't configured, logs
        // a warning (so a missing Render env var gets noticed quickly) and
        // swaps the bank-detail lines for a plain-language fallback instead
        // of emailing a client blanks or leftover placeholder text.
        private string BuildPaymentDetailsHtml(Client client, Booking booking, Payment? payment)
        {
            var amountLine = payment != null ? $"Amount due: R{payment.Amount}<br>" : string.Empty;

            string bankDetailsLines;
            if (HasCompletePaymentDetails())
            {
                bankDetailsLines = $"""
                    Account holder: {_paymentOptions.AccountHolder}<br>
                    Bank: {_paymentOptions.Bank}<br>
                    Account number: {_paymentOptions.AccountNumber}<br>
                    Reference: {client.FullName} {booking.BookingId}<br>
                    """;
            }
            else
            {
                _logger.LogWarning(
                    "Payment account details are not fully configured (Payment:AccountHolder/Bank/AccountNumber) - " +
                    "booking {BookingId}'s confirmation email will not include them.",
                    booking.BookingId);
                bankDetailsLines = "The studio will send you payment details separately.<br>";
            }

            return $"""
                <p><strong>Payment</strong><br>
                {amountLine}{bankDetailsLines}
                Cash is also accepted at class.</p>
                """;
        }

        private bool HasCompletePaymentDetails()
        {
            return !string.IsNullOrWhiteSpace(_paymentOptions.AccountHolder)
                && !string.IsNullOrWhiteSpace(_paymentOptions.Bank)
                && !string.IsNullOrWhiteSpace(_paymentOptions.AccountNumber);
        }

        // The day-before reminder, sent by ScheduledNotificationService's
        // hourly sweep rather than triggered by anything a client or admin
        // does directly.
        public async Task SendReminderEmailAsync(Booking booking)
        {
            var client = await _clientRepository.GetByIdAsync(booking.ClientId);
            if (client == null)
            {
                _logger.LogWarning("SendReminderEmailAsync: client {ClientId} not found for booking {BookingId}", booking.ClientId, booking.BookingId);
                return;
            }

            var session = await _sessionRepository.GetByIdAsync(booking.SessionId);
            var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;

            var html = $"""
                <p>Hi {client.FullName},</p>
                <p>Just a reminder that your class is tomorrow:</p>
                <ul>
                    <li><strong>Class:</strong> {session?.SessionType}</li>
                    <li><strong>Date:</strong> {session?.Date.ToString("yyyy-MM-dd")}</li>
                    <li><strong>Time:</strong> {session?.Time.ToString(@"hh\:mm")}</li>
                    <li><strong>Venue:</strong> {location?.Name} - {location?.Address}</li>
                </ul>
                <p>See you then!</p>
                """;

            await _emailSender.SendEmailAsync(client.Email, "Reminder: your class is tomorrow", html);
            await WriteNotificationRecordAsync(booking.BookingId, "Reminder");
        }

        // Morning-of "here's exactly where to go" email, sent only to new
        // clients on the day of their first class - also driven by the
        // scheduled sweep, not a direct user action.
        public async Task SendLocationEmailAsync(Booking booking)
        {
            var client = await _clientRepository.GetByIdAsync(booking.ClientId);
            if (client == null)
            {
                _logger.LogWarning("SendLocationEmailAsync: client {ClientId} not found for booking {BookingId}", booking.ClientId, booking.BookingId);
                return;
            }

            var session = await _sessionRepository.GetByIdAsync(booking.SessionId);
            var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;

            var html = $"""
                <p>Hi {client.FullName},</p>
                <p>Since this is your first class with us, here's exactly where to go today:</p>
                <ul>
                    <li><strong>Venue:</strong> {location?.Name}</li>
                    <li><strong>Address:</strong> {location?.Address}</li>
                    <li><strong>Time:</strong> {session?.Time.ToString(@"hh\:mm")}</li>
                </ul>
                <p>We're looking forward to seeing you!</p>
                """;

            await _emailSender.SendEmailAsync(client.Email, "Today's class: where to find us", html);
            await WriteNotificationRecordAsync(booking.BookingId, "Location");
        }

        // Sent when PromoteNextInLineAsync turns a waitlist entry into a
        // real booking - includes the same FR-18 cancellation link as a
        // normal confirmation, since this new booking needs one too.
        public async Task SendWaitlistNotificationAsync(Booking booking)
        {
            var client = await _clientRepository.GetByIdAsync(booking.ClientId);
            if (client == null)
            {
                _logger.LogWarning("SendWaitlistNotificationAsync: client {ClientId} not found for booking {BookingId}", booking.ClientId, booking.BookingId);
                return;
            }

            var session = await _sessionRepository.GetByIdAsync(booking.SessionId);
            var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;
            var cancelUrl = $"{_appBaseUrl}/Booking/Cancel/{booking.CancellationToken}";

            var html = $"""
                <p>Hi {client.FullName},</p>
                <p>Good news - a spot opened up and you've been moved off the waitlist into this class:</p>
                <ul>
                    <li><strong>Class:</strong> {session?.SessionType}</li>
                    <li><strong>Date:</strong> {session?.Date.ToString("yyyy-MM-dd")}</li>
                    <li><strong>Time:</strong> {session?.Time.ToString(@"hh\:mm")}</li>
                    <li><strong>Venue:</strong> {location?.Name} - {location?.Address}</li>
                </ul>
                <p>Need to cancel? <a href="{cancelUrl}">Cancel this booking</a>.</p>
                """;

            await _emailSender.SendEmailAsync(client.Email, "You're off the waitlist!", html);
            await WriteNotificationRecordAsync(booking.BookingId, "WaitlistPromotion");
        }

        // The one-off "you're close" email WaitlistService.NotifyAsync
        // triggers - unlike the other Send*Async methods this takes a
        // Waitlist entry rather than a Booking, since nothing has been
        // booked yet.
        public async Task SendWaitlistCourtesyEmailAsync(Waitlist entry)
        {
            var client = await _clientRepository.GetByIdAsync(entry.ClientId);
            if (client == null)
            {
                _logger.LogWarning("SendWaitlistCourtesyEmailAsync: client {ClientId} not found for waitlist entry {WaitlistId}", entry.ClientId, entry.WaitlistId);
                return;
            }

            var session = await _sessionRepository.GetByIdAsync(entry.SessionId);
            var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;

            var html = $"""
                <p>Hi {client.FullName},</p>
                <p>You're number {entry.Position} on the waitlist for this class, and a spot may be opening up soon:</p>
                <ul>
                    <li><strong>Class:</strong> {session?.SessionType}</li>
                    <li><strong>Date:</strong> {session?.Date.ToString("yyyy-MM-dd")}</li>
                    <li><strong>Time:</strong> {session?.Time.ToString(@"hh\:mm")}</li>
                    <li><strong>Venue:</strong> {location?.Name} - {location?.Address}</li>
                </ul>
                <p>We'll email you again if a spot is confirmed for you.</p>
                """;

            // No WriteNotificationRecordAsync here - that helper keys its
            // record off a BookingId, and this entry doesn't have one yet.
            await _emailSender.SendEmailAsync(client.Email, "You're on the waitlist - a spot may be opening up", html);
        }

        // Sent only for an admin-initiated cancellation (see
        // BookingService.CancelAsync's notifyClient flag) - a client who
        // cancelled via their own link already knows.
        public async Task SendCancellationEmailAsync(Booking booking)
        {
            var client = await _clientRepository.GetByIdAsync(booking.ClientId);
            if (client == null)
            {
                _logger.LogWarning("SendCancellationEmailAsync: client {ClientId} not found for booking {BookingId}", booking.ClientId, booking.BookingId);
                return;
            }

            var session = await _sessionRepository.GetByIdAsync(booking.SessionId);
            var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;

            var html = $"""
                <p>Hi {client.FullName},</p>
                <p>Your booking has been cancelled by Thrive Wellness:</p>
                <ul>
                    <li><strong>Class:</strong> {session?.SessionType}</li>
                    <li><strong>Date:</strong> {session?.Date.ToString("yyyy-MM-dd")}</li>
                    <li><strong>Time:</strong> {session?.Time.ToString(@"hh\:mm")}</li>
                    <li><strong>Venue:</strong> {location?.Name} - {location?.Address}</li>
                </ul>
                <p>If you have any questions or would like to rebook, please get in touch or visit
                our schedule page.</p>
                """;

            await _emailSender.SendEmailAsync(client.Email, "Your Thrive Wellness booking has been cancelled", html);
            await WriteNotificationRecordAsync(booking.BookingId, "Cancellation");
        }

        // Logs that a given email type went out for a booking, so
        // ScheduledNotificationService's "has this already been sent" checks
        // have something to query against.
        private async Task WriteNotificationRecordAsync(int bookingId, string type)
        {
            _context.Notifications.Add(new Notification
            {
                BookingId = bookingId,
                Type = type,
                SentAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }
}
