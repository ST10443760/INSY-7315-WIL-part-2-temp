using System.Collections.Concurrent;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: online card payment for a booking. Checks the booking
    // belongs to the signed-in client and can still be paid, charges the
    // card through IPaymentGateway, records every attempt as a
    // PaymentTransaction, and on success hands over to the existing
    // PaymentService.ConfirmPaymentAsync - so the PaymentConfirmed event and
    // its "you're booked in" email work exactly as they do when the admin
    // confirms an EFT or cash payment. The card itself only passes through
    // here on its way to the gateway; it's never stored or logged.
    public class CheckoutService : ICheckoutService
    {
        // Payments with a charge in flight right now. A second submit for
        // the same payment (a double click that got past the disabled
        // button, two tabs) is turned away instead of charging twice.
        // Static because the service is created per request.
        private static readonly ConcurrentDictionary<int, byte> PaymentsInFlight = new();

        private readonly IBookingRepository _bookingRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;
        private readonly IPaymentGateway _gateway;
        private readonly IPaymentService _paymentService;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CheckoutService> _logger;

        public CheckoutService(
            IBookingRepository bookingRepository,
            ISessionRepository sessionRepository,
            ILocationRepository locationRepository,
            IPaymentRepository paymentRepository,
            IPaymentTransactionRepository transactionRepository,
            IPaymentGateway gateway,
            IPaymentService paymentService,
            TimeProvider timeProvider,
            ILogger<CheckoutService> logger)
        {
            _bookingRepository = bookingRepository;
            _sessionRepository = sessionRepository;
            _locationRepository = locationRepository;
            _paymentRepository = paymentRepository;
            _transactionRepository = transactionRepository;
            _gateway = gateway;
            _paymentService = paymentService;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<CheckoutResult> GetCheckoutAsync(int bookingId, int clientId)
        {
            var (result, _) = await LoadAsync(bookingId, clientId);
            return result;
        }

        public async Task<CheckoutResult> PayAsync(int bookingId, int clientId, CardDetails card, CancellationToken cancellationToken = default)
        {
            var (result, payment) = await LoadAsync(bookingId, clientId);
            if (result.Status != CheckoutStatus.Ready || payment == null)
            {
                return result;
            }

            if (!PaymentsInFlight.TryAdd(payment.PaymentId, 0))
            {
                result.Status = CheckoutStatus.InProgress;
                result.Message = "Your payment is already being processed. Please wait a moment.";
                return result;
            }

            try
            {
                // A charge that went through earlier but whose confirmation
                // didn't finish (the app stopped in between) must never be
                // charged again - finish confirming it instead.
                var earlierCharge = await _transactionRepository.GetSucceededForPaymentAsync(payment.PaymentId);
                if (earlierCharge != null)
                {
                    await ConfirmAsync(payment.PaymentId, bookingId);
                    return AlreadyPaid(result, payment.PaymentId);
                }

                GatewayChargeResult charge;
                try
                {
                    charge = await _gateway.ChargeAsync(new GatewayChargeRequest
                    {
                        Amount = payment.Amount,
                        Currency = result.Summary!.Currency,
                        BookingReference = $"Booking {bookingId}",
                        Card = card
                    }, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The provider couldn't be reached, so nothing was
                    // charged. Only the exception type is logged - a
                    // provider's message is never trusted not to echo the
                    // request.
                    _logger.LogWarning(
                        "The payment gateway could not be reached for booking {BookingId} ({ExceptionType}).",
                        bookingId, ex.GetType().Name);

                    result.Status = CheckoutStatus.Declined;
                    result.Message = "We couldn't reach the payment provider. You haven't been charged - please try again.";
                    return result;
                }

                await _transactionRepository.AddAsync(new PaymentTransaction
                {
                    PaymentId = payment.PaymentId,
                    Amount = payment.Amount,
                    Currency = result.Summary!.Currency,
                    Status = charge.Success ? PaymentTransactionStatus.Succeeded : PaymentTransactionStatus.Declined,
                    Brand = charge.Brand,
                    Last4 = charge.Last4,
                    GatewayReference = charge.GatewayReference,
                    FailureCode = charge.FailureCode,
                    FailureMessage = charge.FailureMessage,
                    CreatedAt = DateTime.UtcNow
                });

                if (!charge.Success)
                {
                    _logger.LogInformation(
                        "Card payment for booking {BookingId} was declined ({FailureCode}).",
                        bookingId, charge.FailureCode);

                    result.Status = CheckoutStatus.Declined;
                    result.Message = charge.FailureMessage ?? "Your card was declined.";
                    return result;
                }

                _logger.LogInformation(
                    "Card payment for booking {BookingId} succeeded ({GatewayReference}).",
                    bookingId, charge.GatewayReference);

                await ConfirmAsync(payment.PaymentId, bookingId);

                result.Status = CheckoutStatus.Succeeded;
                result.PaymentId = payment.PaymentId;
                return result;
            }
            finally
            {
                PaymentsInFlight.TryRemove(payment.PaymentId, out _);
            }
        }

        public async Task<ReceiptResult> GetReceiptAsync(int paymentId, int clientId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            var booking = payment != null ? await _bookingRepository.GetByIdAsync(payment.BookingId) : null;
            if (payment == null || booking == null)
            {
                return new ReceiptResult { Status = ReceiptStatus.NotFound };
            }

            if (booking.ClientId != clientId)
            {
                return new ReceiptResult { Status = ReceiptStatus.NotOwner };
            }

            var charge = await _transactionRepository.GetSucceededForPaymentAsync(paymentId);
            if (charge == null)
            {
                return new ReceiptResult { Status = ReceiptStatus.NotPaid };
            }

            var session = await _sessionRepository.GetByIdAsync(booking.SessionId);
            var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;

            return new ReceiptResult
            {
                Status = ReceiptStatus.Ready,
                Receipt = new ReceiptViewModel
                {
                    PaymentId = payment.PaymentId,
                    BookingId = booking.BookingId,
                    Amount = charge.Amount,
                    Currency = charge.Currency,
                    PaidAt = charge.CreatedAt,
                    GatewayReference = charge.GatewayReference,
                    Brand = charge.Brand,
                    Last4 = charge.Last4,
                    PaymentStatus = payment.Status,
                    SessionType = session?.SessionType ?? string.Empty,
                    SessionDate = session?.Date ?? default,
                    SessionTime = session?.Time ?? default,
                    LocationName = location?.Name ?? string.Empty,
                    LocationAddress = location?.Address ?? string.Empty
                }
            };
        }

        // The checks every checkout request shares: the booking exists, is
        // this client's, is a card booking, isn't cancelled, isn't paid
        // already, and its class hasn't started. Returns the payment too
        // when the booking is ready to be charged.
        private async Task<(CheckoutResult Result, Payment? Payment)> LoadAsync(int bookingId, int clientId)
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking == null)
            {
                return (new CheckoutResult { Status = CheckoutStatus.NotFound }, null);
            }

            if (booking.ClientId != clientId)
            {
                return (new CheckoutResult { Status = CheckoutStatus.NotOwner }, null);
            }

            var payment = await _paymentRepository.GetByBookingIdAsync(bookingId);
            var session = await _sessionRepository.GetByIdAsync(booking.SessionId);
            if (payment == null || session == null)
            {
                return (new CheckoutResult { Status = CheckoutStatus.NotFound }, null);
            }

            var location = await _locationRepository.GetByIdAsync(session.LocationId);
            var result = new CheckoutResult
            {
                Summary = new CheckoutSummary
                {
                    BookingId = booking.BookingId,
                    PaymentId = payment.PaymentId,
                    SessionType = session.SessionType,
                    SessionDate = session.Date,
                    SessionTime = session.Time,
                    LocationName = location?.Name ?? string.Empty,
                    LocationAddress = location?.Address ?? string.Empty,
                    PaymentType = payment.PaymentType,
                    Amount = payment.Amount
                }
            };

            if (payment.Status == PaymentStatuses.Confirmed)
            {
                return (AlreadyPaid(result, payment.PaymentId), null);
            }

            if (booking.Status == "Cancelled")
            {
                return (NotPayable(result, "This booking has been cancelled, so there's nothing to pay."), null);
            }

            if (!PaymentMethods.IsCard(payment.Method))
            {
                return (NotPayable(result, "This booking is set to be paid by EFT or cash, not by card."), null);
            }

            if (payment.Status != PaymentStatuses.Pending)
            {
                return (NotPayable(result, "This booking can't be paid online."), null);
            }

            var classStarts = session.Date.Date + session.Time;
            if (classStarts <= _timeProvider.GetLocalNow().DateTime)
            {
                return (NotPayable(result, "This class has already started, so it can no longer be paid online."), null);
            }

            result.Status = CheckoutStatus.Ready;
            return (result, payment);
        }

        // ConfirmPaymentAsync updates the payment and booking first and then
        // raises PaymentConfirmed, whose email can fail. The card has been
        // charged by then and the booking is confirmed, so a failed email is
        // logged (NotificationService has already recorded it as Failed) and
        // the client still gets their receipt.
        private async Task ConfirmAsync(int paymentId, int bookingId)
        {
            try
            {
                await _paymentService.ConfirmPaymentAsync(paymentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Card payment for booking {BookingId} was taken, but confirming it raised {ExceptionType}: {ExceptionMessage}",
                    bookingId, ex.GetType().Name, ex.Message);
            }
        }

        private static CheckoutResult AlreadyPaid(CheckoutResult result, int paymentId)
        {
            result.Status = CheckoutStatus.AlreadyPaid;
            result.PaymentId = paymentId;
            return result;
        }

        private static CheckoutResult NotPayable(CheckoutResult result, string message)
        {
            result.Status = CheckoutStatus.NotPayable;
            result.Message = message;
            return result;
        }
    }
}
