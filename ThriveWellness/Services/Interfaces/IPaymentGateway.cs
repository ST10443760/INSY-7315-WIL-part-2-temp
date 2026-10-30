using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    // The one seam between the app and whoever actually moves the money.
    // Checkout only ever talks to this interface, so swapping the simulated
    // gateway for PayFast or Stripe means a new implementation and one DI
    // line in Program.cs, nothing else.
    public interface IPaymentGateway
    {
        // True when no real money can move (a provider's sandbox, or the
        // simulated gateway). The checkout shows a small "Test mode" label
        // and the test card numbers only while this is true.
        bool IsTestMode { get; }

        // Charges the card. A declined or invalid card is a normal result
        // (Success = false with a failure code and message), not an
        // exception.
        Task<GatewayChargeResult> ChargeAsync(GatewayChargeRequest request, CancellationToken cancellationToken = default);

        // Gives back an earlier charge, identified by the reference the
        // provider returned for it.
        Task<GatewayRefundResult> RefundAsync(string gatewayReference, decimal amount, CancellationToken cancellationToken = default);
    }
}
