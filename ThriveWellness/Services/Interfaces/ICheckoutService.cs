using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface ICheckoutService
    {
        // Whether this client can pay for this booking by card right now,
        // and the order summary if so.
        Task<CheckoutResult> GetCheckoutAsync(int bookingId, int clientId);

        // Charges the card through IPaymentGateway and, on success, confirms
        // the payment through IPaymentService.
        Task<CheckoutResult> PayAsync(int bookingId, int clientId, CardDetails card, CancellationToken cancellationToken = default);

        Task<ReceiptResult> GetReceiptAsync(int paymentId, int clientId);
    }
}
