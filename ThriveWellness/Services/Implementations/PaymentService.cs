using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IBookingRepository _bookingRepository;

        public event EventHandler<PaymentConfirmedEventArgs>? PaymentConfirmed;

        public PaymentService(IPaymentRepository paymentRepository, IBookingRepository bookingRepository)
        {
            _paymentRepository = paymentRepository;
            _bookingRepository = bookingRepository;
        }

        public async Task ConfirmPaymentAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
            {
                throw new ArgumentException($"Payment {paymentId} not found.");
            }

            await _paymentRepository.UpdateStatusAsync(paymentId, "Confirmed");

            var booking = await _bookingRepository.GetByIdAsync(payment.BookingId);
            if (booking == null)
            {
                throw new InvalidOperationException($"Booking {payment.BookingId} for payment {paymentId} not found.");
            }

            booking.Status = "Confirmed";
            await _bookingRepository.UpdateAsync(booking);

            // TODO: this synchronously blocks ConfirmPaymentAsync (and the
            // admin's request thread) until NotificationService.OnPaymentConfirmed
            // returns - fine for the old Console.WriteLine stub, but
            // OnPaymentConfirmed now makes a real SendGrid call with real
            // network latency. Move this to a background/queued send (e.g.
            // enqueue the notification and let a hosted service process it)
            // so a slow SendGrid response doesn't hold up payment confirmation.
            PaymentConfirmed?.Invoke(this, new PaymentConfirmedEventArgs(booking.BookingId, booking.ClientId));
        }
    }
}
