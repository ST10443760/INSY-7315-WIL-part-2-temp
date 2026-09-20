using Microsoft.Extensions.Logging;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IClientRepository _clientRepository;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IBookingRepository bookingRepository,
            IClientRepository clientRepository,
            ILogger<NotificationService> logger)
        {
            _bookingRepository = bookingRepository;
            _clientRepository = clientRepository;
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
            // connection-aborted errors when this ran undetached. This
            // stub is fast (a couple of reads + a log line), so blocking
            // synchronously here is fine; a real SendGrid call should be
            // queued onto its own scope instead of reusing this one.
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

            // TODO: replace this stub with a real SendGrid call once the
            // email integration feature lands.
            _logger.LogInformation("Would send welcome email to {Email}", client?.Email);
        }
    }
}
