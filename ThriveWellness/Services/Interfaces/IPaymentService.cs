using ThriveWellness.Services;

namespace ThriveWellness.Services.Interfaces
{
    public interface IPaymentService
    {
        event EventHandler<PaymentConfirmedEventArgs>? PaymentConfirmed;

        Task ConfirmPaymentAsync(int paymentId);
    }
}
