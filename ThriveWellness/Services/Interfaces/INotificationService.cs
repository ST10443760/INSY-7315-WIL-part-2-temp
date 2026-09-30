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
        Task SendWaitlistNotificationAsync(Booking booking);
        Task SendCancellationEmailAsync(Booking booking);

        // Admin-triggered "a spot might be opening up" nudge to one specific
        // waitlisted client - distinct from SendWaitlistNotificationAsync,
        // which fires automatically once that client has actually been
        // promoted into a real booking. This one has no side effects on the
        // waitlist or booking state, just an email.
        Task SendWaitlistCourtesyEmailAsync(Waitlist entry);

        // Matches EventHandler<PaymentConfirmedEventArgs> so Program.cs can
        // wire it to IPaymentService.PaymentConfirmed without depending on
        // the concrete NotificationService type.
        void OnPaymentConfirmed(object? sender, PaymentConfirmedEventArgs e);
    }
}
