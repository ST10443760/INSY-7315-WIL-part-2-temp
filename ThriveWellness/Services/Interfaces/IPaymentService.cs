using ThriveWellness.Services;

namespace ThriveWellness.Services.Interfaces
{
    public interface IPaymentService
    {
        event EventHandler<PaymentConfirmedEventArgs>? PaymentConfirmed;

        // True if the payment and booking were confirmed and the "you're
        // booked in" email went out; false if the email failed (the
        // confirmation still stands).
        Task<bool> ConfirmPaymentAsync(int paymentId);

        // Refund due -> Refunded, once the studio has paid the client back.
        Task<bool> MarkRefundedAsync(int paymentId);
    }
}
