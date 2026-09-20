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
            // Fire-and-forget: this stub is synchronous-fast (a log line), so
            // discarding the task is fine here. A real SendGrid call should
            // be queued rather than awaited from a void event handler.
            _ = HandlePaymentConfirmedAsync(e);
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
