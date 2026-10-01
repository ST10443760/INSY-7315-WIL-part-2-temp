using Moq;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services;
using ThriveWellness.Services.Implementations;

namespace ThriveWellness.Tests;

public class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IBookingRepository> _bookingRepository = new();

    private PaymentService CreateService() => new(_paymentRepository.Object, _bookingRepository.Object);

    private static Payment PendingPayment() => new()
    {
        PaymentId = 1,
        BookingId = 10,
        Method = "EFT",
        Amount = 120m,
        Status = "Pending",
        PaymentType = "per-class"
    };

    private static Booking AwaitingBooking() => new()
    {
        BookingId = 10,
        ClientId = 3,
        SessionId = 1,
        Status = "Awaiting Payment",
        CancellationToken = "token"
    };

    [Fact]
    public async Task ConfirmPaymentAsync_UpdatesBothPaymentAndBookingStatus()
    {
        _paymentRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingPayment());
        var booking = AwaitingBooking();
        _bookingRepository.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(booking);

        var service = CreateService();
        await service.ConfirmPaymentAsync(1);

        _paymentRepository.Verify(r => r.UpdateStatusAsync(1, "Confirmed"), Times.Once);
        Assert.Equal("Confirmed", booking.Status);
        _bookingRepository.Verify(r => r.UpdateAsync(It.Is<Booking>(b => b.BookingId == 10 && b.Status == "Confirmed")), Times.Once);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_RaisesPaymentConfirmedEventWithTheRightIds()
    {
        _paymentRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingPayment());
        _bookingRepository.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(AwaitingBooking());

        var service = CreateService();

        PaymentConfirmedEventArgs? received = null;
        var handlerCallCount = 0;
        service.PaymentConfirmed += (sender, args) =>
        {
            handlerCallCount++;
            received = args;
        };

        await service.ConfirmPaymentAsync(1);

        Assert.Equal(1, handlerCallCount);
        Assert.NotNull(received);
        Assert.Equal(10, received!.BookingId);
        Assert.Equal(3, received.ClientId);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_SubscribedHandlerThatThrowsStillLetsStatusUpdatesStand()
    {
        // The event fires after both status updates are already persisted
        // (see PaymentService's own TODO about this being synchronous), so
        // a misbehaving subscriber shouldn't roll anything back - it just
        // propagates past ConfirmPaymentAsync's own caller, same as any
        // other unhandled exception would.
        _paymentRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(PendingPayment());
        _bookingRepository.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(AwaitingBooking());

        var service = CreateService();
        service.PaymentConfirmed += (_, _) => throw new InvalidOperationException("subscriber blew up");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmPaymentAsync(1));

        _paymentRepository.Verify(r => r.UpdateStatusAsync(1, "Confirmed"), Times.Once);
        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_UnknownPayment_ThrowsAndNeverTouchesABooking()
    {
        _paymentRepository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Payment?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ConfirmPaymentAsync(99));

        _bookingRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }
}
