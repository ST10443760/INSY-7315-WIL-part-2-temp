using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

public class CheckoutServiceTests
{
    private const int ClientId = 7;
    private const int OtherClientId = 8;

    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<ISessionRepository> _sessionRepository = new();
    private readonly Mock<ILocationRepository> _locationRepository = new();
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IPaymentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly List<PaymentTransaction> _savedTransactions = new();

    private readonly Booking _booking;
    private readonly Payment _payment;
    private readonly Session _session;

    // The real PaymentService, so these tests prove the existing
    // PaymentConfirmed event is what a card payment goes through.
    private readonly PaymentService _paymentService;
    private int _paymentConfirmedCount;

    // 2 November 2026, 09:00 - the session below is three days later.
    private class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private readonly FixedClock _clock = new();

    public CheckoutServiceTests()
    {
        _booking = new Booking { BookingId = 42, ClientId = ClientId, SessionId = 3, Status = "Awaiting Payment" };
        _payment = new Payment
        {
            PaymentId = 9,
            BookingId = 42,
            Method = PaymentMethods.Card,
            Amount = PaymentPricing.PerClassPrice,
            Status = PaymentStatuses.Pending,
            PaymentType = "per-class"
        };
        _session = new Session
        {
            SessionId = 3,
            LocationId = 1,
            SessionType = "group",
            Date = new DateTime(2026, 11, 5),
            Time = new TimeSpan(10, 0, 0),
            Capacity = 10,
            IsOpen = true
        };

        _bookingRepository.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(() => _booking);
        _bookingRepository.Setup(r => r.UpdateAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);
        _sessionRepository.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(_session);
        _locationRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Location { LocationId = 1, Name = "Test Studio", Address = "Test Suburb" });
        _paymentRepository.Setup(r => r.GetByBookingIdAsync(42)).ReturnsAsync(() => _payment);
        _paymentRepository.Setup(r => r.GetByIdAsync(9)).ReturnsAsync(() => _payment);
        _paymentRepository
            .Setup(r => r.UpdateStatusAsync(9, It.IsAny<string>()))
            .Callback<int, string>((_, status) => _payment.Status = status)
            .Returns(Task.CompletedTask);

        _transactionRepository
            .Setup(r => r.AddAsync(It.IsAny<PaymentTransaction>()))
            .Callback<PaymentTransaction>(t => _savedTransactions.Add(t))
            .Returns(Task.CompletedTask);
        _transactionRepository
            .Setup(r => r.GetSucceededForPaymentAsync(9))
            .ReturnsAsync(() => _savedTransactions.LastOrDefault(t => t.Status == PaymentTransactionStatus.Succeeded));

        _paymentService = new PaymentService(_paymentRepository.Object, _bookingRepository.Object);
        _paymentService.PaymentConfirmed += (_, _) => _paymentConfirmedCount++;
    }

    private CheckoutService CreateService() => new(
        _bookingRepository.Object,
        _sessionRepository.Object,
        _locationRepository.Object,
        _paymentRepository.Object,
        _transactionRepository.Object,
        _gateway.Object,
        _paymentService,
        _clock,
        NullLogger<CheckoutService>.Instance);

    private static CardDetails Card(string number = "4242424242424242") => new()
    {
        CardholderName = "Test Client",
        Number = number,
        ExpiryMonth = 12,
        ExpiryYear = 2030,
        Cvc = "123"
    };

    private void GatewayReturns(GatewayChargeResult result) =>
        _gateway
            .Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private static GatewayChargeResult Approved() => new()
    {
        Success = true,
        GatewayReference = "TW-TEST000001",
        Brand = CardBrands.Visa,
        Last4 = "4242"
    };

    private static GatewayChargeResult Declined() => new()
    {
        Success = false,
        GatewayReference = "TW-TEST000002",
        Brand = CardBrands.Visa,
        Last4 = "0002",
        FailureCode = "card_declined",
        FailureMessage = "Your card was declined."
    };

    [Fact]
    public async Task PayAsync_Success_RecordsTheChargeAndConfirmsThePaymentRaisingTheEventOnce()
    {
        GatewayReturns(Approved());

        var result = await CreateService().PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.Succeeded, result.Status);
        Assert.Equal(9, result.PaymentId);
        var transaction = Assert.Single(_savedTransactions);
        Assert.Equal(PaymentTransactionStatus.Succeeded, transaction.Status);
        Assert.Equal(9, transaction.PaymentId);
        Assert.Equal(PaymentPricing.PerClassPrice, transaction.Amount);
        Assert.Equal("ZAR", transaction.Currency);
        Assert.Equal("TW-TEST000001", transaction.GatewayReference);
        Assert.Equal("4242", transaction.Last4);
        Assert.Equal(PaymentStatuses.Confirmed, _payment.Status);
        Assert.Equal("Confirmed", _booking.Status);
        Assert.Equal(1, _paymentConfirmedCount);
    }

    [Fact]
    public async Task PayAsync_ChargesTheBookingsAmountInRandsWithTheBookingReference()
    {
        GatewayChargeRequest? sent = null;
        _gateway
            .Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GatewayChargeRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(Approved());

        await CreateService().PayAsync(42, ClientId, Card());

        Assert.NotNull(sent);
        Assert.Equal(PaymentPricing.PerClassPrice, sent.Amount);
        Assert.Equal("ZAR", sent.Currency);
        Assert.Equal("Booking 42", sent.BookingReference);
    }

    [Fact]
    public async Task PayAsync_Declined_RecordsADeclineAndLeavesTheBookingAwaitingPayment()
    {
        GatewayReturns(Declined());

        var result = await CreateService().PayAsync(42, ClientId, Card("4000000000000002"));

        Assert.Equal(CheckoutStatus.Declined, result.Status);
        Assert.Equal("Your card was declined.", result.Message);
        var transaction = Assert.Single(_savedTransactions);
        Assert.Equal(PaymentTransactionStatus.Declined, transaction.Status);
        Assert.Equal("card_declined", transaction.FailureCode);
        Assert.Equal(PaymentStatuses.Pending, _payment.Status);
        Assert.Equal("Awaiting Payment", _booking.Status);
        Assert.Equal(0, _paymentConfirmedCount);
    }

    [Fact]
    public async Task PayAsync_DeclineThenRetry_KeepsBothAttemptsAndConfirmsOnce()
    {
        var service = CreateService();

        GatewayReturns(Declined());
        await service.PayAsync(42, ClientId, Card("4000000000000002"));
        GatewayReturns(Approved());
        var result = await service.PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.Succeeded, result.Status);
        Assert.Equal(
            new[] { PaymentTransactionStatus.Declined, PaymentTransactionStatus.Succeeded },
            _savedTransactions.Select(t => t.Status));
        Assert.Equal(1, _paymentConfirmedCount);
    }

    [Fact]
    public async Task PayAsync_AlreadyPaid_IsNotChargedAgain()
    {
        _payment.Status = PaymentStatuses.Confirmed;

        var result = await CreateService().PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.AlreadyPaid, result.Status);
        Assert.Equal(9, result.PaymentId);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_savedTransactions);
        Assert.Equal(0, _paymentConfirmedCount);
    }

    [Fact]
    public async Task PayAsync_SecondSubmitAfterSuccess_IsNotChargedAgain()
    {
        GatewayReturns(Approved());
        var service = CreateService();

        await service.PayAsync(42, ClientId, Card());
        var second = await service.PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.AlreadyPaid, second.Status);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, _paymentConfirmedCount);
    }

    [Fact]
    public async Task PayAsync_ChargeTakenButNotYetConfirmed_FinishesConfirmingWithoutCharging()
    {
        _savedTransactions.Add(new PaymentTransaction { PaymentId = 9, Status = PaymentTransactionStatus.Succeeded, GatewayReference = "TW-EARLIER0001" });

        var result = await CreateService().PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.AlreadyPaid, result.Status);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(PaymentStatuses.Confirmed, _payment.Status);
        Assert.Equal(1, _paymentConfirmedCount);
    }

    [Fact]
    public async Task PayAsync_AnotherClientsBooking_IsRefusedAndNotCharged()
    {
        var result = await CreateService().PayAsync(42, OtherClientId, Card());

        Assert.Equal(CheckoutStatus.NotOwner, result.Status);
        Assert.Null(result.Summary);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(PaymentStatuses.Pending, _payment.Status);
    }

    [Fact]
    public async Task GetCheckoutAsync_AnotherClientsBooking_IsRefused()
    {
        var result = await CreateService().GetCheckoutAsync(42, OtherClientId);

        Assert.Equal(CheckoutStatus.NotOwner, result.Status);
        Assert.Null(result.Summary);
    }

    [Fact]
    public async Task GetCheckoutAsync_UnknownBooking_IsNotFound()
    {
        var result = await CreateService().GetCheckoutAsync(999, ClientId);

        Assert.Equal(CheckoutStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task PayAsync_ClassAlreadyStarted_CannotBePaid()
    {
        _clock.Now = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        var result = await CreateService().PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.NotPayable, result.Status);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PayAsync_CancelledBooking_CannotBePaid()
    {
        _booking.Status = "Cancelled";

        var result = await CreateService().PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.NotPayable, result.Status);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PayAsync_EftBooking_CannotBePaidByCard()
    {
        _payment.Method = PaymentMethods.Eft;

        var result = await CreateService().PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.NotPayable, result.Status);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PayAsync_ConfirmationEmailFails_PaymentStillSucceeds()
    {
        GatewayReturns(Approved());
        _paymentService.PaymentConfirmed += (_, _) => throw new InvalidOperationException("email provider down");

        var result = await CreateService().PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.Succeeded, result.Status);
        Assert.Equal(PaymentStatuses.Confirmed, _payment.Status);
    }

    [Fact]
    public async Task PayAsync_GatewayUnreachable_IsAFriendlyFailureWithNothingRecorded()
    {
        _gateway
            .Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var result = await CreateService().PayAsync(42, ClientId, Card());

        Assert.Equal(CheckoutStatus.Declined, result.Status);
        Assert.Contains("haven't been charged", result.Message);
        Assert.Empty(_savedTransactions);
        Assert.Equal(PaymentStatuses.Pending, _payment.Status);
    }

    [Fact]
    public async Task PayAsync_WithTheSimulatedGateway_TestCardsBehaveEndToEnd()
    {
        var service = new CheckoutService(
            _bookingRepository.Object, _sessionRepository.Object, _locationRepository.Object,
            _paymentRepository.Object, _transactionRepository.Object,
            new SimulatedCardGateway(_clock, TimeSpan.Zero), _paymentService, _clock,
            NullLogger<CheckoutService>.Instance);

        var declined = await service.PayAsync(42, ClientId, Card("4000000000009995"));
        var paid = await service.PayAsync(42, ClientId, Card("5555555555554444"));

        Assert.Equal("Insufficient funds.", declined.Message);
        Assert.Equal(CheckoutStatus.Succeeded, paid.Status);
        Assert.Equal(CardBrands.Mastercard, _savedTransactions.Last().Brand);
        Assert.Equal("4444", _savedTransactions.Last().Last4);
    }

    [Fact]
    public async Task GetReceiptAsync_OwnersPaidBooking_ShowsTheChargeDetails()
    {
        GatewayReturns(Approved());
        var service = CreateService();
        await service.PayAsync(42, ClientId, Card());

        var result = await service.GetReceiptAsync(9, ClientId);

        Assert.Equal(ReceiptStatus.Ready, result.Status);
        Assert.Equal("TW-TEST000001", result.Receipt!.GatewayReference);
        Assert.Equal(CardBrands.Visa, result.Receipt.Brand);
        Assert.Equal("4242", result.Receipt.Last4);
        Assert.Equal("Test Studio", result.Receipt.LocationName);
    }

    [Fact]
    public async Task GetReceiptAsync_AnotherClient_IsRefused()
    {
        GatewayReturns(Approved());
        var service = CreateService();
        await service.PayAsync(42, ClientId, Card());

        var result = await service.GetReceiptAsync(9, OtherClientId);

        Assert.Equal(ReceiptStatus.NotOwner, result.Status);
        Assert.Null(result.Receipt);
    }

    [Fact]
    public async Task GetReceiptAsync_UnpaidBooking_HasNoReceipt()
    {
        var result = await CreateService().GetReceiptAsync(9, ClientId);

        Assert.Equal(ReceiptStatus.NotPaid, result.Status);
    }
}
