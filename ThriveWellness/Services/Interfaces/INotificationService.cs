using ThriveWellness.Models;
using ThriveWellness.Services;

namespace ThriveWellness.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendWelcomeEmailAsync(Booking booking);
        Task SendConfirmationEmailAsync(Booking booking);
        Task SendReminderEmailAsync(Booking booking);
        Task SendLocationEmailAsync(Booking booking);

        // Matches EventHandler<PaymentConfirmedEventArgs> so Program.cs can
        // wire it to IPaymentService.PaymentConfirmed without depending on
        // the concrete NotificationService type.
        void OnPaymentConfirmed(object? sender, PaymentConfirmedEventArgs e);
    }
}
