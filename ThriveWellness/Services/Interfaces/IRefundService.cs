using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IRefundService
    {
        // What cancelling would do to this payment, without doing it - for
        // the "are you sure" page.
        RefundResult Preview(Payment? payment);

        // Refunds (or marks Refund due) the payment of a booking that has
        // just been cancelled. Never throws for a failed refund: it falls
        // back to Refund due so the studio sees it.
        Task<RefundResult> RefundCancelledBookingAsync(int bookingId);
    }
}
