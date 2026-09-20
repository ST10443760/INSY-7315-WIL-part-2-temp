using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
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
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IBookingRepository bookingRepository,
            IClientRepository clientRepository,
            ISessionRepository sessionRepository,
            ILocationRepository locationRepository,
            IPaymentRepository paymentRepository,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IConfiguration configuration,
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

        private async Task HandlePaymentConfirmedAsync(PaymentConfirmedEventArgs e)
        {
            var booking = await _bookingRepository.GetByIdAsync(e.BookingId);
            if (booking != null)
            {
                await SendWelcomeEmailAsync(booking);
            }
        }

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

            var html = $"""
                <p>Hi {client.FullName},</p>
                <p>Your booking is in. Here are the details:</p>
                <ul>
                    <li><strong>Class:</strong> {session?.SessionType}</li>
                    <li><strong>Date:</strong> {session?.Date.ToString("yyyy-MM-dd")}</li>
                    <li><strong>Time:</strong> {session?.Time.ToString(@"hh\:mm")}</li>
                    <li><strong>Venue:</strong> {location?.Name} - {location?.Address}</li>
                    <li><strong>Amount due:</strong> R{payment?.Amount}</li>
                </ul>
                <p><strong>To pay by EFT:</strong> Thrive Wellness, Account 123456789, Branch code 000000.
                Please use your name as the payment reference.</p>
                <p><strong>Prefer cash?</strong> That's fine too - you can pay in person before your class.</p>
                <p>Need to cancel? <a href="{cancelUrl}">Cancel this booking</a>.</p>
                """;

            await _emailSender.SendEmailAsync(client.Email, "Your Thrive Wellness booking is confirmed", html);
            await WriteNotificationRecordAsync(booking.BookingId, "Confirmation");
        }

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
