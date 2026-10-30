using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: what happens to the money when a paid booking is
    // cancelled. A card payment goes back through IPaymentGateway.RefundAsync
    // and its charge is marked Refunded; an EFT or cash payment is marked
    // Refund due, which the admin Payments page shows, since the studio has
    // to pay it back by hand. An unpaid booking has nothing to refund.
    public class RefundService : IRefundService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;
        private readonly IPaymentGateway _gateway;
        private readonly ILogger<RefundService> _logger;

        public RefundService(
            IPaymentRepository paymentRepository,
            IPaymentTransactionRepository transactionRepository,
            IPaymentGateway gateway,
            ILogger<RefundService> logger)
        {
            _paymentRepository = paymentRepository;
            _transactionRepository = transactionRepository;
            _gateway = gateway;
            _logger = logger;
        }

        public RefundResult Preview(Payment? payment)
        {
            if (payment == null || payment.Status != PaymentStatuses.Confirmed)
            {
                return RefundResult.Nothing;
            }

            return new RefundResult
            {
                Outcome = PaymentMethods.IsCard(payment.Method) ? RefundOutcome.CardRefunded : RefundOutcome.RefundDue,
                Amount = payment.Amount
            };
        }

        public async Task<RefundResult> RefundCancelledBookingAsync(int bookingId)
        {
            var payment = await _paymentRepository.GetByBookingIdAsync(bookingId);
            if (payment == null || payment.Status != PaymentStatuses.Confirmed)
            {
                return RefundResult.Nothing;
            }

            if (PaymentMethods.IsCard(payment.Method) && await TryRefundCardAsync(payment, bookingId))
            {
                await _paymentRepository.UpdateStatusAsync(payment.PaymentId, PaymentStatuses.Refunded);
                return new RefundResult { Outcome = RefundOutcome.CardRefunded, Amount = payment.Amount };
            }

            await _paymentRepository.UpdateStatusAsync(payment.PaymentId, PaymentStatuses.RefundDue);
            return new RefundResult { Outcome = RefundOutcome.RefundDue, Amount = payment.Amount };
        }

        // Refunds the charge that paid this payment and marks it Refunded.
        // False if there's no charge on record or the gateway didn't refund
        // it - the caller then falls back to Refund due.
        private async Task<bool> TryRefundCardAsync(Payment payment, int bookingId)
        {
            var charge = await _transactionRepository.GetChargeForPaymentAsync(payment.PaymentId);
            if (charge == null || charge.Status != PaymentTransactionStatus.Succeeded)
            {
                return false;
            }

            GatewayRefundResult refund;
            try
            {
                refund = await _gateway.RefundAsync(charge.GatewayReference, charge.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "The card refund for booking {BookingId} could not be sent ({ExceptionType}); it is marked Refund due instead.",
                    bookingId, ex.GetType().Name);
                return false;
            }

            if (!refund.Success)
            {
                _logger.LogWarning(
                    "The card refund for booking {BookingId} was not accepted; it is marked Refund due instead.",
                    bookingId);
                return false;
            }

            charge.Status = PaymentTransactionStatus.Refunded;
            await _transactionRepository.UpdateAsync(charge);

            _logger.LogInformation(
                "Refunded card payment {GatewayReference} for cancelled booking {BookingId} ({RefundReference}).",
                charge.GatewayReference, bookingId, refund.RefundReference);
            return true;
        }
    }
}
